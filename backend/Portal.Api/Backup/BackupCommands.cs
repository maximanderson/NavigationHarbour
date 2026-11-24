using Portal.Api.Configuration;
using Portal.Api.Configuration.Server;

namespace Portal.Api.Backup;

/// <summary>
/// Command-line handling for --backup-db, --list-backups, --verify-backup and --restore-db.
/// Database-independent: it only talks to <see cref="BackupRegistry"/> and <see cref="BackupService"/>.
/// None of these commands starts the web server. Exit codes: 0 success, 2 refused or failed.
/// </summary>
public static class BackupCommands
{
    public const int ExitFailed = 2;

    public static async Task<int> RunAsync(
        StartupOptions options,
        BackupRegistry registry,
        BackupService service,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = registry.Resolve(options.Database);

            if (options.ListBackups) return List(provider, service, output);
            if (options.BackupDb) return await BackupAsync(provider, service, output, cancellationToken);
            if (options.VerifyBackup is not null) return await VerifyAsync(provider, service, options.VerifyBackup, output, cancellationToken);
            return await RestoreAsync(provider, service, options, output, error, cancellationToken);
        }
        catch (BackupException ex)
        {
            error.WriteLine($"Error: {ex.Message}");
            return ExitFailed;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Cancelled. No backup was published and the live database was not changed.");
            return ExitFailed;
        }
    }

    private static async Task<int> BackupAsync(IDatabaseBackupProvider provider, BackupService service, TextWriter output, CancellationToken ct)
    {
        output.WriteLine($"Backing up database \"{provider.DatabaseId}\" ({provider.ProviderType}): {provider.Description}");
        var entry = await service.CreateAsync(provider, "manual", ct);

        output.WriteLine($"Backup created and verified: {entry.FilePath}");
        output.WriteLine($"  Size   : {entry.Metadata!.SizeBytes} bytes");
        output.WriteLine($"  SHA-256: {entry.Metadata.Sha256}");
        return 0;
    }

    private static int List(IDatabaseBackupProvider provider, BackupService service, TextWriter output)
    {
        var entries = service.Store.List(provider);
        output.WriteLine($"Backups of database \"{provider.DatabaseId}\" in {service.Store.DirectoryPath}");

        if (entries.Count == 0)
        {
            output.WriteLine("  (none)");
            return 0;
        }

        foreach (var entry in entries)
        {
            var m = entry.Metadata;
            var created = m is null ? "unknown time" : m.CreatedUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'");
            var size = m?.SizeBytes ?? new FileInfo(entry.FilePath).Length;
            var reason = m?.Reason ?? "?";
            var note = entry.Problem is null ? "" : $"  [WARNING: {entry.Problem}]";

            output.WriteLine($"  {Path.GetFileName(entry.FilePath)}  {created}  {size,10} bytes  {reason}{note}");
        }

        output.WriteLine("Use --verify-backup <file> to check one; a plain file name is looked up in this directory.");
        return 0;
    }

    private static async Task<int> VerifyAsync(
        IDatabaseBackupProvider provider, BackupService service, string reference, TextWriter output, CancellationToken ct)
    {
        var result = await service.VerifyAsync(provider, reference, ct);

        output.WriteLine($"Backup is valid: {service.Store.Resolve(reference)}");
        output.WriteLine(result.ChecksumChecked
            ? "  Checksum matches its metadata; database integrity check passed."
            : "  No metadata file: the checksum could not be compared. Database integrity check passed.");
        return 0;
    }

    private static async Task<int> RestoreAsync(
        IDatabaseBackupProvider provider,
        BackupService service,
        StartupOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken ct)
    {
        var reference = options.RestoreDb!;
        var file = await service.CheckRestoreAsync(provider, reference, ct);

        output.WriteLine($"Restore of database \"{provider.DatabaseId}\" from {file}");
        output.WriteLine($"  Live database : {provider.Description}");
        output.WriteLine($"  Checks passed : the backup is verified and the maintenance lock is free ({ProductInfo.Name} is not running).");

        if (!options.Yes)
        {
            error.WriteLine();
            error.WriteLine("Nothing was changed. This replaces the live database with the backup.");
            error.WriteLine("A safety backup of the current database is taken first. Run again with --yes to confirm.");
            return ExitFailed;
        }

        var result = await service.RestoreAsync(provider, reference, ct);

        if (result.SafetyBackup is not null)
        {
            output.WriteLine($"  Safety backup : {result.SafetyBackup.FilePath}");
        }
        else
        {
            output.WriteLine("  Safety backup : not needed (there was no database to replace)");
        }

        output.WriteLine($"Restore completed. Start {ProductInfo.Name} again.");
        return 0;
    }
}
