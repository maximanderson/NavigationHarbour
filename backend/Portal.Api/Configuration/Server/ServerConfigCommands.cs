namespace Portal.Api.Configuration.Server;

/// <summary>
/// Output for --check-config and --print-config, and the startup failure message.
/// Writes to the supplied writers only; never starts Kestrel or touches the database.
/// </summary>
public static class ServerConfigCommands
{
    public static int Run(StartupOptions startup, ServerConfigReport report, TextWriter output)
    {
        WriteHeader(report, output);

        if (startup.PrintConfig)
        {
            WriteValues(report, output);
        }

        WriteMessages(report, output);

        // A database problem stops normal startup, so --check-config must not report OK for it.
        var databaseBlocked = report.IsValid && report.Database?.Blocker is not null;
        if (databaseBlocked && startup.CheckConfig)
        {
            output.WriteLine();
            output.WriteLine($"  ERROR   Database: {report.Database!.Blocker}");
        }

        if (startup.CheckConfig)
        {
            output.WriteLine();
            output.WriteLine(databaseBlocked
                ? "Result: INVALID (database; normal startup would stop)"
                : report.IsValid
                ? $"Result: OK ({report.Warnings.Count} warning(s))"
                : $"Result: INVALID ({report.Errors.Count} error(s), {report.Warnings.Count} warning(s))");
        }
        else if (!report.IsValid)
        {
            output.WriteLine();
            output.WriteLine($"Configuration has {report.Errors.Count} error(s).");
        }

        return report.IsValid && !(databaseBlocked && startup.CheckConfig) ? 0 : ServerConfiguration.ExitInvalid;
    }

    /// <summary>Message printed to stderr when startup is refused because of the database.</summary>
    public static void WriteDatabaseFailure(ServerConfigReport report, TextWriter error)
    {
        var db = report.Database!;
        error.WriteLine($"{ProductInfo.Name} cannot start: database problem.");
        error.WriteLine($"  Environment : {report.EnvironmentName}");
        error.WriteLine($"  Database    : {db.FilePath} ({db.StateText})");
        error.WriteLine($"  Location from: {db.Origin}");
        error.WriteLine($"  {db.Blocker}");
        error.WriteLine("No database was created or changed.");
    }

    /// <summary>Message printed to stderr when normal startup is refused.</summary>
    public static void WriteStartupFailure(ServerConfigReport report, TextWriter error)
    {
        error.WriteLine($"{ProductInfo.Name} cannot start: invalid configuration.");
        error.WriteLine($"  Environment: {report.EnvironmentName}");
        error.WriteLine($"  Config file: {report.ConfigPath ?? "(unknown: home directory not resolved)"}");
        error.WriteLine($"  Content root: {report.ContentRoot ?? "(unknown)"}");
        WriteMessages(report, error, includeNotices: false);
        error.WriteLine("Run with --check-config for details, or --print-config to see the effective values.");
    }

    private static void WriteHeader(ServerConfigReport report, TextWriter output)
    {
        output.WriteLine($"{ProductInfo.Name} server configuration");
        output.WriteLine($"  Environment : {report.EnvironmentName}");
        output.WriteLine($"  Home        : {report.Home.Path ?? "(not resolved)"} [{report.Home.Source}]");
        output.WriteLine($"  Content root: {report.ContentRoot ?? "(unknown)"} [shipped appsettings*.json; separate from Home]");
        output.WriteLine($"  Config file : {report.ConfigPath ?? "(unknown)"} [{(report.FileFound ? "found" : "not found")}]");

        output.WriteLine(report.Settings.ExplicitEndpoint
            ? $"  Endpoint    : {KestrelSetup.Describe(report.Settings)} [from [server]; one listener]"
            : "  Endpoint    : not configured by [server]; ASPNETCORE_URLS / launchSettings.json decide");

        if (report.Database is { FilePath: not null } db)
        {
            output.WriteLine($"  Database    : {db.FilePath} [{db.StateText}]");
            output.WriteLine($"                from {db.Origin}; {db.PolicyText}");
        }
    }

    private static void WriteValues(ServerConfigReport report, TextWriter output)
    {
        output.WriteLine();
        output.WriteLine("Effective settings (value  <- source)");

        foreach (var group in report.Values.GroupBy(v => v.Section))
        {
            output.WriteLine($"[{group.Key}]");
            var width = group.Max(v => v.Key.Length);

            foreach (var item in group)
            {
                var state = item.Applied ? "applied" : "validated only";
                output.WriteLine($"  {item.Key.PadRight(width)} = {item.Value}  <- {item.Source}  [{state}]");
                if (item.ResolvedPath is not null)
                {
                    output.WriteLine($"  {string.Empty.PadRight(width)}   resolves to {item.ResolvedPath}");
                }
            }
        }

        output.WriteLine();
        output.WriteLine("applied        = used by the running application now.");
        output.WriteLine("validated only = checked, but not applied (for [server] and [tls]: no endpoint setting is configured, so");
        output.WriteLine("                 ASPNETCORE_URLS / launchSettings.json decide; [paths] public is applied when its folders exist).");
        output.WriteLine($"Certificates are only checked here (settings and files); they are loaded when {ProductInfo.Name} starts. Passwords are never shown.");
        output.WriteLine("Source 'default' for [log] and [paths] means the appsettings.json defaults are used (see docs).");
    }

    private static void WriteMessages(ServerConfigReport report, TextWriter output, bool includeNotices = true)
    {
        if (includeNotices && report.Notices.Count > 0)
        {
            output.WriteLine();
            foreach (var notice in report.Notices) output.WriteLine($"  NOTE    {notice}");
        }

        if (report.Warnings.Count > 0)
        {
            output.WriteLine();
            foreach (var warning in report.Warnings) output.WriteLine($"  WARNING {warning}");
        }

        if (report.Errors.Count > 0)
        {
            output.WriteLine();
            foreach (var error in report.Errors) output.WriteLine($"  ERROR   {error}");
        }
    }
}
