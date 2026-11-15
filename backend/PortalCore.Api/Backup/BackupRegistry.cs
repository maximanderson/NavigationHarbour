using System.Text.RegularExpressions;

namespace PortalCore.Api.Backup;

/// <summary>
/// The registered backup providers, selected by database identifier. With a single provider the
/// identifier may be omitted. Adding a database means registering one more provider.
/// </summary>
public sealed partial class BackupRegistry
{
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,31}$")]
    private static partial Regex IdPattern();

    public IReadOnlyList<IDatabaseBackupProvider> Providers { get; }

    public BackupRegistry(IEnumerable<IDatabaseBackupProvider> providers)
    {
        var list = providers.ToList();

        foreach (var provider in list)
        {
            if (!IdPattern().IsMatch(provider.DatabaseId))
            {
                throw new ArgumentException(
                    $"Database identifier \"{provider.DatabaseId}\" is invalid; use 1-32 lower-case letters, digits, '-' or '_'.");
            }
        }

        var duplicate = list.GroupBy(p => p.DatabaseId, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Database identifier \"{duplicate.Key}\" is registered more than once.");
        }

        Providers = list;
    }

    /// <summary>Returns the provider for <paramref name="id"/>, or the only provider when no id is given.</summary>
    public IDatabaseBackupProvider Resolve(string? id)
    {
        var known = string.Join(", ", Providers.Select(p => p.DatabaseId));

        if (string.IsNullOrWhiteSpace(id))
        {
            return Providers.Count switch
            {
                1 => Providers[0],
                0 => throw new BackupException("No database is registered for backup."),
                _ => throw new BackupException($"More than one database is registered; choose one with --database <id>. Available: {known}."),
            };
        }

        return Providers.FirstOrDefault(p => string.Equals(p.DatabaseId, id.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new BackupException($"Unknown database \"{id.Trim()}\". Available: {known}.");
    }
}
