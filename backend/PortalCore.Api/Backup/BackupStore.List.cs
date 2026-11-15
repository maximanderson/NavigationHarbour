using System.Text.RegularExpressions;

namespace PortalCore.Api.Backup;

public sealed partial class BackupStore
{
    /// <summary>Lists the backups of one database, newest first. Reads metadata but does not hash files.</summary>
    public IReadOnlyList<BackupEntry> List(IDatabaseBackupProvider provider)
    {
        if (!System.IO.Directory.Exists(DirectoryPath))
        {
            return Array.Empty<BackupEntry>();
        }

        var pattern = new Regex(
            $"^{Regex.Escape(provider.DatabaseId)}-\\d{{8}}T\\d{{6}}Z(-\\d+)?{Regex.Escape(provider.BackupFileExtension)}$",
            RegexOptions.IgnoreCase);

        var entries = new List<BackupEntry>();

        foreach (var file in System.IO.Directory.EnumerateFiles(DirectoryPath))
        {
            if (!pattern.IsMatch(Path.GetFileName(file)))
            {
                continue;
            }

            var metadata = ReadMetadata(file, out var problem);

            if (metadata is not null)
            {
                if (!string.Equals(metadata.DatabaseId, provider.DatabaseId, StringComparison.OrdinalIgnoreCase))
                {
                    problem = $"metadata belongs to database \"{metadata.DatabaseId}\"";
                }
                else if (new FileInfo(file).Length != metadata.SizeBytes)
                {
                    problem = "size differs from metadata (file changed or incomplete)";
                }
            }

            entries.Add(new BackupEntry(file, metadata, problem));
        }

        return entries
            .OrderByDescending(e => e.Metadata?.CreatedUtc ?? new DateTimeOffset(File.GetLastWriteTimeUtc(e.FilePath), TimeSpan.Zero))
            .ThenByDescending(e => e.FilePath, StringComparer.Ordinal)
            .ToList();
    }
}
