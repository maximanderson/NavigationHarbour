namespace Portal.Api.Configuration.Server;

/// <summary>
/// Navigation Harbour's own command-line switches. They are parsed (and removed) before the
/// arguments reach ASP.NET Core, so the standard command-line configuration provider
/// never sees them and cannot mistake the value of "--home" for a separate switch.
/// </summary>
public sealed class StartupOptions
{
    public bool CheckConfig { get; private set; }
    public bool PrintConfig { get; private set; }

    /// <summary>--init-db: deliberately create a new empty database (never touches an existing one).</summary>
    public bool InitDb { get; private set; }

    // Backup commands (Stage 3B.1). At most one may be given.
    public bool BackupDb { get; private set; }
    public bool ListBackups { get; private set; }

    /// <summary>--verify-backup &lt;file&gt;</summary>
    public string? VerifyBackup { get; private set; }

    /// <summary>--restore-db &lt;file&gt;</summary>
    public string? RestoreDb { get; private set; }

    /// <summary>--yes: confirms a restore. Without it a restore only checks and reports.</summary>
    public bool Yes { get; private set; }

    /// <summary>--database &lt;id&gt;: which database a backup command applies to (default: the only one).</summary>
    public string? Database { get; private set; }

    public bool IsBackupCommand => BackupDb || ListBackups || VerifyBackup is not null || RestoreDb is not null;

    /// <summary>Allows a missing server.ini outside Development (testing only).</summary>
    public bool AllowMissingConfig { get; private set; }

    /// <summary>Value of --home, if given.</summary>
    public string? Home { get; private set; }

    /// <summary>Arguments left for ASP.NET Core (everything that is not a Navigation Harbour switch).</summary>
    public string[] HostArgs { get; private set; } = Array.Empty<string>();

    /// <summary>Problems with Navigation Harbour's own switches.</summary>
    public List<string> Errors { get; } = new();

    public bool IsDiagnosticCommand => CheckConfig || PrintConfig;

    public static StartupOptions Parse(string[] args)
    {
        var options = new StartupOptions();
        var remaining = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (Is(arg, "--check-config"))
            {
                options.CheckConfig = true;
            }
            else if (Is(arg, "--print-config"))
            {
                options.PrintConfig = true;
            }
            else if (Is(arg, "--init-db"))
            {
                options.InitDb = true;
            }
            else if (Is(arg, "--allow-missing-config"))
            {
                options.AllowMissingConfig = true;
            }
            else if (Is(arg, "--backup-db"))
            {
                options.BackupDb = true;
            }
            else if (Is(arg, "--list-backups"))
            {
                options.ListBackups = true;
            }
            else if (Is(arg, "--yes"))
            {
                options.Yes = true;
            }
            else if (Is(arg, "--verify-backup"))
            {
                options.VerifyBackup = ReadValue(args, ref i, "--verify-backup", "a backup file name", options.Errors);
            }
            else if (Is(arg, "--restore-db"))
            {
                options.RestoreDb = ReadValue(args, ref i, "--restore-db", "a backup file name", options.Errors);
            }
            else if (Is(arg, "--database"))
            {
                options.Database = ReadValue(args, ref i, "--database", "a database id, for example: --database portal", options.Errors);
            }
            else if (Is(arg, "--home"))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options.Errors.Add("--home requires a directory, for example: --home /opt/navigationharbour");
                }
                else
                {
                    options.Home = args[++i];
                }
            }
            else if (arg.StartsWith("--home=", StringComparison.OrdinalIgnoreCase))
            {
                options.Home = arg["--home=".Length..];
            }
            else
            {
                remaining.Add(arg);
            }
        }

        if (options.Home is not null && string.IsNullOrWhiteSpace(options.Home))
        {
            options.Errors.Add("--home requires a directory, for example: --home /opt/navigationharbour");
            options.Home = null;
        }

        var commands = new[] { options.BackupDb, options.ListBackups, options.VerifyBackup is not null, options.RestoreDb is not null }
            .Count(c => c);
        if (commands > 1)
        {
            options.Errors.Add("Use only one of --backup-db, --list-backups, --verify-backup and --restore-db at a time.");
        }

        if (options.IsBackupCommand && options.InitDb)
        {
            options.Errors.Add("--init-db cannot be combined with a backup command.");
        }

        options.HostArgs = remaining.ToArray();
        return options;
    }

    private static string? ReadValue(string[] args, ref int i, string name, string what, List<string> errors)
    {
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            errors.Add($"{name} requires {what}.");
            return null;
        }

        return args[++i];
    }

    private static bool Is(string arg, string name) =>
        string.Equals(arg, name, StringComparison.OrdinalIgnoreCase);
}
