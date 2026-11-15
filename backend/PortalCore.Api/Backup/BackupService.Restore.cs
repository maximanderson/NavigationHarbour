namespace PortalCore.Api.Backup;

public sealed partial class BackupService
{
    /// <summary>Verifies a backup file: metadata and checksum when present, then the provider's own check.</summary>
    public async Task<VerifyResult> VerifyAsync(
        IDatabaseBackupProvider provider,
        string reference,
        CancellationToken cancellationToken)
    {
        var file = _store.Resolve(reference);
        EnsureUsableBackupFile(provider, file);

        var metadata = BackupStore.ReadMetadata(file, out _);
        var checksumChecked = false;

        if (metadata is not null)
        {
            if (!string.Equals(metadata.DatabaseId, provider.DatabaseId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(metadata.ProviderType, provider.ProviderType, StringComparison.OrdinalIgnoreCase))
            {
                throw new BackupException(
                    $"The backup belongs to database \"{metadata.DatabaseId}\" ({metadata.ProviderType}), not \"{provider.DatabaseId}\" ({provider.ProviderType}).");
            }

            if (new FileInfo(file).Length != metadata.SizeBytes ||
                !string.Equals(BackupStore.Sha256Of(file), metadata.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new BackupException("The backup file does not match its recorded checksum; it was changed or is damaged.");
            }

            checksumChecked = true;
        }

        await provider.VerifyAsync(file, cancellationToken);
        return new VerifyResult(metadata, checksumChecked);
    }

    /// <summary>
    /// Everything a restore checks before it changes anything: the backup is usable and verified, and the
    /// exclusive maintenance lock can be taken (Portal Core is not running). The lock is released again;
    /// nothing is changed. Returns the resolved backup file.
    /// </summary>
    public async Task<string> CheckRestoreAsync(
        IDatabaseBackupProvider provider,
        string reference,
        CancellationToken cancellationToken)
    {
        using var maintenanceLock = provider.AcquireMaintenanceLock(createDirectory: false);
        return await CheckLockedAsync(provider, reference, cancellationToken);
    }

    private async Task<string> CheckLockedAsync(
        IDatabaseBackupProvider provider,
        string reference,
        CancellationToken cancellationToken)
    {
        _store.EnsureSafeFor(provider);

        var file = _store.Resolve(reference);
        await VerifyAsync(provider, reference, cancellationToken);
        await provider.EnsureRestoreAllowedAsync(cancellationToken);
        return file;
    }

    /// <summary>
    /// Restores a backup over the live database. The exclusive maintenance lock is taken first and held until
    /// the end, so Portal Core cannot start using the database in between. Then: verify the backup, take a
    /// verified pre-restore backup of the current database, and only then replace it.
    /// Any failure before the replacement leaves the live database exactly as it was.
    /// </summary>
    public async Task<RestoreResult> RestoreAsync(
        IDatabaseBackupProvider provider,
        string reference,
        CancellationToken cancellationToken)
    {
        using var maintenanceLock = provider.AcquireMaintenanceLock(createDirectory: true);

        var file = await CheckLockedAsync(provider, reference, cancellationToken);

        BackupEntry? safety = null;
        if (provider.DatabaseExists)
        {
            // If this fails nothing has been changed and the restore is abandoned.
            safety = await CreateAsync(provider, "pre-restore", cancellationToken);
        }

        await provider.RestoreAsync(file, cancellationToken);
        return new RestoreResult(file, safety);
    }

    private static void EnsureUsableBackupFile(IDatabaseBackupProvider provider, string file)
    {
        if (!File.Exists(file))
        {
            throw new BackupException($"Backup file not found: {file}");
        }

        if (file.EndsWith(BackupStore.PartialSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new BackupException("That is an unfinished backup (.partial) and cannot be used.");
        }

        if (provider.ProtectedPaths.Any(p => string.Equals(Path.GetFullPath(p), file, PathComparison)))
        {
            throw new BackupException("That is the live database, not a backup.");
        }
    }
}
