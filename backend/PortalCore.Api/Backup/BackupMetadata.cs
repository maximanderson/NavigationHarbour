namespace PortalCore.Api.Backup;

/// <summary>
/// Describes one backup. Stored next to the backup file as "&lt;backup file&gt;.meta.json".
/// Contains no paths, connection strings or other secrets.
/// </summary>
public sealed class BackupMetadata
{
    public int FormatVersion { get; set; } = 1;

    /// <summary>Stable database identifier, e.g. "portal".</summary>
    public string DatabaseId { get; set; } = "";

    /// <summary>Database kind, e.g. "sqlite".</summary>
    public string ProviderType { get; set; } = "";

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>File name (no directory) of the backup this metadata describes.</summary>
    public string BackupFile { get; set; } = "";

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 of the backup file, lower-case hex.</summary>
    public string Sha256 { get; set; } = "";

    public string ApplicationVersion { get; set; } = "";

    /// <summary>Why the backup was taken: "manual", "pre-restore" and later "pre-migration".</summary>
    public string Reason { get; set; } = "manual";

    /// <summary>The provider's own verification passed before the backup was published.</summary>
    public bool Verified { get; set; }
}

/// <summary>A backup file found in the backup directory.</summary>
/// <param name="FilePath">Full path of the backup file.</param>
/// <param name="Metadata">Null when no (readable) metadata file exists.</param>
/// <param name="Problem">Why the entry is not fully trustworthy, or null.</param>
public sealed record BackupEntry(string FilePath, BackupMetadata? Metadata, string? Problem);

public sealed record VerifyResult(BackupMetadata? Metadata, bool ChecksumChecked);

public sealed record RestoreResult(string RestoredFrom, BackupEntry? SafetyBackup);
