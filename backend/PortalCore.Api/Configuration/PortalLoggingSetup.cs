using Serilog;
using Serilog.Debugging;
using Serilog.Events;

namespace PortalCore.Api.Configuration;

/// <summary>
/// Builds the Serilog logger from <see cref="PortalLoggingOptions"/>.
/// Serilog replaces the default ASP.NET Core logging providers, so the legacy
/// "Logging:LogLevel" section no longer applies and messages are not duplicated.
/// </summary>
public static class PortalLoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    public static Serilog.ILogger CreateLogger(
        PortalLoggingOptions options,
        string contentRootPath,
        string? webRootPath,
        List<string> startupIssues)
    {
        // Sink failures (e.g. disk full, permission denied) are reported on stderr
        // by Serilog itself and never propagate into request handling.
        SelfLog.Enable(System.Console.Error);

        var config = new LoggerConfiguration();

        if (!options.Enabled)
        {
            // No sinks: diagnostic logging is off.
            return config.CreateLogger();
        }

        // Framework namespaces stay at Warning or above (matches the previous
        // appsettings), which also keeps outgoing HTTP URLs and SQL out of the logs.
        var frameworkLevel = options.Level > LogEventLevel.Warning ? options.Level : LogEventLevel.Warning;

        config
            .MinimumLevel.Is(options.Level)
            .MinimumLevel.Override("Microsoft", frameworkLevel)
            .MinimumLevel.Override("System", frameworkLevel)
            // Unhandled exceptions are logged once by the app's own handler (Program.cs);
            // silence the framework's duplicate entry (it can also embed request fragments).
            .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
            .Enrich.FromLogContext();

        if (options.Console)
        {
            config.WriteTo.Console(outputTemplate: OutputTemplate);
        }

        if (options.FileEnabled)
        {
            try
            {
                var directory = ResolveDirectory(options, contentRootPath, webRootPath, startupIssues);
                System.IO.Directory.CreateDirectory(directory);

                config.WriteTo.File(
                    path: Path.Combine(directory, "portalcore-.log"),
                    outputTemplate: OutputTemplate,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: options.MaxFileSizeMb * 1024L * 1024L,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: null,
                    retainedFileTimeLimit: TimeSpan.FromDays(options.RetainDays));
            }
            catch (Exception ex)
            {
                // Continue with the remaining destinations.
                startupIssues.Add($"File logging disabled: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return config.CreateLogger();
    }

    /// <summary>
    /// Resolves the log directory relative to the content root and refuses any
    /// location inside the publicly served web root.
    /// </summary>
    private static string ResolveDirectory(
        PortalLoggingOptions options,
        string contentRootPath,
        string? webRootPath,
        List<string> startupIssues)
    {
        var resolved = Path.GetFullPath(Path.Combine(contentRootPath, options.Directory));

        if (!string.IsNullOrEmpty(webRootPath) && IsUnder(resolved, Path.GetFullPath(webRootPath)))
        {
            var fallback = Path.GetFullPath(Path.Combine(contentRootPath, PortalLoggingOptions.DefaultDirectory));
            startupIssues.Add(
                $"Log directory must not be inside the public web root; using \"{PortalLoggingOptions.DefaultDirectory}\".");
            return fallback;
        }

        return resolved;
    }

    private static bool IsUnder(string path, string root)
    {
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return (path + Path.DirectorySeparatorChar).StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
