using System.Reflection;

namespace PortalCore.Api.Backup;

/// <summary>
/// Backup orchestration shared by every database: naming, temporary-then-publish, verification,
/// metadata, restore safety. It contains no database-specific code; that is the provider's job.
/// </summary>
public sealed partial class BackupService
{
    private readonly BackupStore _store;
    private readonly TimeProvider _time;
    private readonly string _applicationVersion;

    public BackupService(BackupStore store, TimeProvider? time = null, string? applicationVersion = null)
    {
        _store = store;
        _time = time ?? TimeProvider.System;
        _applicationVersion = applicationVersion ?? CurrentVersion();
    }

    public BackupStore Store => _store;

    public static string CurrentVersion() =>
        typeof(BackupService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(BackupService).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>
    /// Takes a verified backup. The backup is written to a temporary file, verified, and only then
    /// published under its final name; a failure leaves no file that looks like a backup.
    /// </summary>
    public async Task<BackupEntry> CreateAsync(
        IDatabaseBackupProvider provider,
        string reason,
        CancellationToken cancellationToken)
    {
        _store.EnsureSafeFor(provider);

        if (!provider.DatabaseExists)
        {
            throw new BackupSourceMissingException(
                $"The {provider.DatabaseId} database does not exist ({provider.Description}). " +
                "A backup never creates a database; there is nothing to back up.");
        }

        _store.EnsureDirectory();
        var (finalFile, tempFile) = _store.Allocate(provider, _time.GetUtcNow());
        var metadataFile = finalFile + BackupStore.MetaSuffix;
        var published = false;

        try
        {
            await provider.BackupAsync(tempFile, cancellationToken);
            await provider.VerifyAsync(tempFile, cancellationToken);

            var metadata = new BackupMetadata
            {
                DatabaseId = provider.DatabaseId,
                ProviderType = provider.ProviderType,
                CreatedUtc = _time.GetUtcNow(),
                BackupFile = Path.GetFileName(finalFile),
                SizeBytes = new FileInfo(tempFile).Length,
                Sha256 = BackupStore.Sha256Of(tempFile),
                ApplicationVersion = _applicationVersion,
                Reason = reason,
                Verified = true,
            };

            // Metadata first, then the backup file: the file only becomes visible once it is described.
            BackupStore.WriteMetadata(metadataFile, metadata);
            File.Move(tempFile, finalFile, overwrite: false);
            published = true;

            return new BackupEntry(finalFile, metadata, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupException($"Backup failed: {ex.GetType().Name}: {ex.Message}", ex);
        }
        finally
        {
            TryDelete(tempFile);
            if (!published)
            {
                TryDelete(metadataFile);
            }
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftover temporary files are never listed or restorable; nothing more to do.
        }
    }
}
