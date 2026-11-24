using System.Security.Cryptography;
using System.Text.Json;
using Portal.Api.Configuration.Server;

namespace Portal.Api.Backup;

/// <summary>
/// Backup directory handling: file naming, metadata files, listing. Database-independent.
/// Backup file:  &lt;id&gt;-&lt;yyyyMMdd&gt;T&lt;HHmmss&gt;Z[-n]&lt;ext&gt;      Metadata:  &lt;backup file&gt;.meta.json
/// Files being written carry the suffix ".partial" and are never listed or restorable.
/// </summary>
public sealed partial class BackupStore
{
    public const string MetaSuffix = ".meta.json";
    public const string PartialSuffix = ".partial";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string DirectoryPath { get; }

    public BackupStore(string directoryPath)
    {
        DirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
    }

    /// <summary>
    /// The backup directory must not be the live database's directory, nor contain it: backups and the
    /// source would be mixed, and a backup could be mistaken for (or overwrite) the live files.
    /// A sub-directory of the database directory is allowed (the development default is
    /// "&lt;project&gt;/backups" next to "&lt;project&gt;/usage.db").
    /// </summary>
    public void EnsureSafeFor(IDatabaseBackupProvider provider)
    {
        foreach (var protectedPath in provider.ProtectedPaths)
        {
            var liveDirectory = Path.GetDirectoryName(Path.GetFullPath(protectedPath))!;
            if (PortalPaths.IsSameOrUnder(liveDirectory, DirectoryPath))
            {
                throw new BackupException(
                    $"The backup directory {DirectoryPath} is, or contains, the live database directory {liveDirectory}. " +
                    "Choose a separate directory with [paths] backups.");
            }
        }
    }

    /// <summary>Creates the backup directory when needed (the only place that does).</summary>
    public void EnsureDirectory()
    {
        try
        {
            System.IO.Directory.CreateDirectory(DirectoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupException($"Cannot create the backup directory {DirectoryPath}: {ex.GetType().Name}.", ex);
        }
    }

    /// <summary>Picks a unique final name and a unique temporary name in the backup directory.</summary>
    public (string Final, string Temp) Allocate(IDatabaseBackupProvider provider, DateTimeOffset now)
    {
        var stamp = now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
        var baseName = $"{provider.DatabaseId}-{stamp}";

        for (var n = 1; n < 1000; n++)
        {
            var name = (n == 1 ? baseName : $"{baseName}-{n}") + provider.BackupFileExtension;
            var final = Path.Combine(DirectoryPath, name);

            if (!File.Exists(final) && !File.Exists(final + MetaSuffix))
            {
                var temp = Path.Combine(DirectoryPath, $".{name}.{Guid.NewGuid().ToString("N")[..8]}{PartialSuffix}");
                return (final, temp);
            }
        }

        throw new BackupException("Too many backups were created in the same second.");
    }

    public static string Sha256Of(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static void WriteMetadata(string metadataFile, BackupMetadata metadata) =>
        File.WriteAllText(metadataFile, JsonSerializer.Serialize(metadata, Json));

    /// <summary>Reads the metadata of a backup file; null with a reason when it is absent or unreadable.</summary>
    public static BackupMetadata? ReadMetadata(string backupFile, out string? problem)
    {
        var path = backupFile + MetaSuffix;
        problem = null;

        if (!File.Exists(path))
        {
            problem = "no metadata file";
            return null;
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<BackupMetadata>(File.ReadAllText(path));
            if (metadata is null || string.IsNullOrWhiteSpace(metadata.DatabaseId))
            {
                problem = "metadata file is incomplete";
                return null;
            }

            return metadata;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            problem = "metadata file is unreadable";
            return null;
        }
    }

    /// <summary>
    /// Turns a command-line reference into a file path: a plain file name is looked up in the backup
    /// directory; anything containing a directory part is used as given (relative to the current directory).
    /// </summary>
    public string Resolve(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new BackupException("A backup file name is required.");
        }

        var value = reference.Trim();
        var isPlainName = value.IndexOfAny(new[] { '/', '\\' }) < 0;
        return Path.GetFullPath(isPlainName ? Path.Combine(DirectoryPath, value) : value);
    }
}
