using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portal.Api.Abstractions;
using Portal.Api.Configuration;
using Portal.Api.Models;
using Portal.Api.Persistence;
using Portal.Api.Readers;
using Portal.Api.Services;
using Portal.Api.Backup;
using Portal.Api.Backup.Sqlite;
using Portal.Api.Configuration.Server;
using Serilog;
// (Portal.Api.Persistence is imported above and provides DatabaseInitializer.)

// Navigation Harbour's own switches (--check-config, --print-config, --home, --allow-missing-config)
// are removed here so ASP.NET Core's command-line provider never sees them.
var startupOptions = StartupOptions.Parse(args);

// Content root is chosen explicitly (project directory in development, the application's bin
// directory when installed), never from the working directory. Otherwise appsettings*.json would
// silently not load when the process is started from another directory, e.g. by a Windows service.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = startupOptions.HostArgs,
    ContentRootPath = PortalHome.ResolveContentRoot(AppContext.BaseDirectory),
});

// Private server configuration (conf/server.ini): adds the INI and PORTAL_/PORTALCORE_ sources and
// validates them. [log], [paths] logs and [paths] ai are mapped onto the existing PortalLogging
// and AiSettings keys; other settings are validated only (later stages).
var environmentHome = PortalHome.ReadEnvironment(Environment.GetEnvironmentVariable);
var serverConfig = ServerConfiguration.Load(
    builder.Configuration,
    startupOptions,
    builder.Environment.EnvironmentName,
    AppContext.BaseDirectory,
    environmentHome.Value,
    builder.Environment.ContentRootPath,
    environmentHome.Name);

// Diagnostic commands stop here: no logging setup, no Kestrel, no database, no services.
if (startupOptions.IsDiagnosticCommand)
{
    return ServerConfigCommands.Run(startupOptions, serverConfig, Console.Out);
}

if (!serverConfig.IsValid)
{
    ServerConfigCommands.WriteStartupFailure(serverConfig, Console.Error);
    return ServerConfiguration.ExitInvalid;
}

var database = serverConfig.Database!;

// Backup commands (--backup-db, --list-backups, --verify-backup, --restore-db). They run before the
// "missing database" check below on purpose: restoring into a missing database is how a lost
// database is recovered. They never start the web server.
if (startupOptions.IsBackupCommand)
{
    if (database.State == DatabaseState.Ambiguous)
    {
        ServerConfigCommands.WriteDatabaseFailure(serverConfig, Console.Error);
        return BackupCommands.ExitFailed;
    }

    if (serverConfig.Settings.BackupsPath is null)
    {
        Console.Error.WriteLine($"Error: the backup directory cannot be determined ({ProductInfo.Name} home is unknown). Use --home <directory>.");
        return BackupCommands.ExitFailed;
    }

    var registry = new BackupRegistry(new IDatabaseBackupProvider[]
    {
        new SqliteBackupProvider("portal", database.FilePath!),
    });
    var backupService = new BackupService(new BackupStore(serverConfig.Settings.BackupsPath));

    using var cancellation = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

    return await BackupCommands.RunAsync(
        startupOptions, registry, backupService, Console.Out, Console.Error, cancellation.Token);
}

// --init-db: the only deliberate way to create a database outside the Development default.
// It never touches an existing database and does not start the web server.
if (startupOptions.InitDb)
{
    return DatabaseInitializer.Run(database, Console.Out, Console.Error);
}

// Database safety: never silently create an empty database in place of a missing one.
if (database.Blocker is not null)
{
    ServerConfigCommands.WriteDatabaseFailure(serverConfig, Console.Error);
    return ServerConfiguration.ExitInvalid;
}

// Listening endpoint (Stage 4). Only when [server] protocol, http_addr or port is configured does Portal
// Core configure Kestrel itself, with exactly one listener; otherwise ASPNETCORE_URLS / launchSettings.json
// apply as before. A missing or unusable certificate stops startup: https never falls back to http.
System.Security.Cryptography.X509Certificates.X509Certificate2? tlsCertificate = null;
if (serverConfig.Settings.ExplicitEndpoint)
{
    if (serverConfig.Settings.Protocol == "https")
    {
        try
        {
            tlsCertificate = TlsCertificateLoader.Load(serverConfig.Settings);
        }
        catch (TlsCertificateException ex)
        {
            Console.Error.WriteLine($"{ProductInfo.Name} cannot start: the HTTPS certificate could not be loaded.");
            Console.Error.WriteLine($"  {ex.Message}");
            Console.Error.WriteLine("No listener was started. Check [tls] in server.ini (run --check-config).");
            return ServerConfiguration.ExitInvalid;
        }
    }

    builder.WebHost.ConfigureKestrel(kestrel => KestrelSetup.Configure(kestrel, serverConfig.Settings, tlsCertificate));
}

// Maintenance lock: the server holds a shared, operating-system-enforced lock on
// "<database>.maintenance.lock" for as long as it runs. A restore needs the same lock exclusively, so it
// refuses to run while this process is alive. The OS releases the lock when the process ends, however it ends.
// (It cannot stop an external SQLite client that ignores the lock; see docs/backup-restore.md.)
MaintenanceLock maintenanceLock;
try
{
    maintenanceLock = MaintenanceLock.AcquireShared(database.FilePath!);
}
catch (BackupException ex)
{
    Console.Error.WriteLine($"Error: {ProductInfo.Name} cannot start: {ex.Message}");
    return ServerConfiguration.ExitInvalid;
}

// System logging (diagnostics only; independent of Usage Monitor and AI analysis).
// Serilog replaces the default providers so messages are written exactly once.
var loggingOptions = PortalLoggingOptions.Parse(builder.Configuration);
var loggingSetupIssues = new List<string>();
var serilogLogger = PortalLoggingSetup.CreateLogger(
    loggingOptions,
    builder.Environment.ContentRootPath,
    builder.Environment.WebRootPath,
    loggingSetupIssues);
builder.Host.UseSerilog(serilogLogger, dispose: true);

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add DbContext
// The connection string is the resolved absolute path with Mode=ReadWrite, so SQLite itself can
// never create a new file at runtime (see ServerConfiguration.Database.cs).
builder.Services.AddDbContext<PortalDbContext>(options =>
    options.UseSqlite(database.RuntimeConnectionString));

// Configure AiSettings
builder.Services.Configure<AiSettings>(
    builder.Configuration.GetSection("AiSettings"));

// Add HttpClient and AiQueryService
builder.Services.AddHttpClient();
builder.Services.AddScoped<AiQueryService>();

// Add Knowledge Retrieval Architecture
// Step 1: Register readers that create KnowledgeChunks from files
builder.Services.AddSingleton<IKnowledgeReader, CsvKnowledgeReader>();
builder.Services.AddSingleton<IKnowledgeReader, SvgKnowledgeReader>();
builder.Services.AddSingleton<IKnowledgeReader, PiVisionJsonKnowledgeReader>();

// Step 2: Register knowledge sources (file sources, future vector DB sources)
builder.Services.AddScoped<IKnowledgeSource, KnowledgeFileSource>();

// Step 3: Register central orchestration service that coordinates all sources
builder.Services.AddScoped<KnowledgeRetrievalService>();

// Add Prompt Template Provider
builder.Services.AddScoped<IPromptTemplateProvider, FilePromptTemplateProvider>();

var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// Client IP behind a reverse proxy: X-Forwarded-For is honoured only when the direct peer is a trusted
// proxy (loopback by default, plus [proxy] trusted_proxies / trusted_networks). See docs/reverse-proxy.md.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(
    options => ForwardedHeadersSetup.Configure(options, serverConfig.Settings));

var app = builder.Build();

// Must run before anything that reads the client address (the usage endpoint, logging, exception handling).
app.UseForwardedHeaders();

// Global exception handling: log the failure, return a safe response
// (no stack trace, no exception message, no secrets).
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    if (feature is not null)
    {
        var errorLogger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Portal.Api.UnhandledException");
        errorLogger.LogError(
            feature.Error,
            "Unhandled exception. Method={Method}, Path={Path}, TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path.Value,
            context.TraceIdentifier);
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
    {
        error = "An unexpected error occurred.",
        traceId = context.TraceIdentifier
    });
}));

app.UseCors("FrontendDev");

// Standalone hosting (Stage 5): the compiled frontend from bin/wwwroot and the /config, /navigation and
// /content folders of [paths] public, by standard static file middleware. In development neither exists
// next to the backend (Vite serves them), so this adds nothing there. Directory browsing is never enabled.
var staticHosting = StaticHosting.Use(app, serverConfig.Settings.PublicPath);

// Enable Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// A new empty database is created only for the Development default location when the file is
// missing. An existing database is opened as-is: no EnsureCreated, no migration.
if (database.CreateOnStartup)
{
    DatabaseInitializer.Create(database);
    app.Logger.LogWarning("Created a new empty development database at {DatabasePath}", database.FilePath);
}

// Startup diagnostics
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    startupLogger.LogInformation(
        "{Product} API starting. Environment={Environment}, LogLevel={LogLevel}, Console={Console}, File={File}",
        ProductInfo.Name,
        app.Environment.EnvironmentName,
        loggingOptions.Level,
        loggingOptions.Console,
        loggingOptions.FileEnabled);

    foreach (var issue in loggingOptions.Issues.Concat(loggingSetupIssues))
    {
        startupLogger.LogWarning("Logging configuration: {Issue}", issue);
    }

    if (serverConfig.Settings.ExplicitEndpoint)
    {
        startupLogger.LogInformation("Listening endpoint from server configuration: {Endpoint}", KestrelSetup.Describe(serverConfig.Settings));

        foreach (var warning in serverConfig.Settings.EndpointWarnings)
        {
            startupLogger.LogWarning("Server configuration: {Warning}", warning);
        }
    }

    var ai = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiSettings>>().Value;
    startupLogger.LogInformation(
        "AI assistant configuration. Enabled={Enabled}, Provider={Provider}",
        ai.Enabled,
        ai.Provider);
}

// POST /api/usage
app.MapPost("/api/usage", async (UsageLog log, PortalDbContext db, HttpContext http) =>
{
    log.TimestampUtc = DateTime.UtcNow;

    if (string.IsNullOrEmpty(log.UserName))
    {
        log.UserName = http.User?.Identity?.Name ?? "anonymous";
    }

    log.ClientIp = http.Connection.RemoteIpAddress?.ToString();

    db.UsageLogs.Add(log);
    await db.SaveChangesAsync();

    return Results.Ok(new { success = true });
})
.WithName("LogUsage");

// POST /api/ai/query
app.MapPost("/api/ai/query", async (
    HttpContext http,
    AiQueryService aiQueryService,
    Microsoft.Extensions.Options.IOptions<AiSettings> aiOptions,
    ILogger<Program> logger) =>
{
    // Experimental AI is disabled by default: answer 404 before reading the body or touching any provider.
    if (!aiOptions.Value.Enabled)
    {
        return Results.NotFound(new { error = "Not found" });
    }

    using var reader = new StreamReader(http.Request.Body);
    var body = await reader.ReadToEndAsync();

    AiRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<AiRequest>(
            body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }
    catch (JsonException)
    {
        // The exception message can quote request content, so it is not logged.
        logger.LogWarning("AI query rejected: request body is not valid JSON");
        return Results.BadRequest(new { error = "Invalid request body" });
    }

    if (string.IsNullOrWhiteSpace(request?.Prompt))
    {
        return Results.BadRequest(new { error = "Prompt is empty" });
    }

    try
    {
        var answer = await aiQueryService.QueryAsync(request.Prompt);
        return Results.Ok(new { answer });
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "AI query failed: provider request error");
        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "AI query failed unexpectedly");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
})
.WithName("AiQuery");

// GET /api/knowledge/search
app.MapGet("/api/knowledge/search", async (
    string? q,
    KnowledgeRetrievalService knowledgeService,
    Microsoft.Extensions.Options.IOptions<AiSettings> aiOptions,
    ILogger<Program> logger) =>
{
    // Knowledge search belongs to the experimental AI subsystem: 404 while it is disabled.
    if (!aiOptions.Value.Enabled)
    {
        return Results.NotFound(new { error = "Not found" });
    }

    if (string.IsNullOrWhiteSpace(q))
    {
        logger.LogWarning("Knowledge search request with empty query");
        return Results.BadRequest(new { error = "Query parameter 'q' is required and cannot be empty" });
    }

    logger.LogDebug("Knowledge search request received. QueryLength={QueryLength}", q.Length);

    try
    {
        var results = await knowledgeService.SearchAsync(q);
        logger.LogInformation("Knowledge search completed. ResultCount={Count}", results.Count);
        return Results.Ok(results);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Knowledge search failed. QueryLength={QueryLength}", q.Length);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
})
.WithName("KnowledgeSearch");

// Unknown /api/** paths answer with a JSON 404; other extension-less paths fall back to the React app.
// A missing file such as /assets/x.js stays a plain 404.
StaticHosting.MapFallback(app);

app.Logger.LogInformation(
    "Static hosting: frontend={Frontend} ({WebRoot}), public folders={PublicFolders} (from {PublicPath})",
    staticHosting.Frontend ? "served" : "not present",
    app.Environment.WebRootPath ?? "no web root",
    staticHosting.PublicFolders.Count == 0 ? "none" : string.Join(", ", staticHosting.PublicFolders),
    serverConfig.Settings.PublicPath ?? "unknown");

try
{
    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    app.Logger.LogCritical(ex, "{Product} API terminated unexpectedly", ProductInfo.Name);
    throw;
}
finally
{
    Log.CloseAndFlush();
    maintenanceLock.Dispose();
}

return 0;

record AiRequest(string Prompt);
