using Microsoft.EntityFrameworkCore;
using PortalCore.Api.Configuration.Server;

namespace PortalCore.Api.Persistence;

/// <summary>
/// The only code that creates a database file. It never touches a database that already exists,
/// never migrates, and is used by exactly two callers: --init-db and the Development default start.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>Creates the schema in a new (missing or zero-byte) database file.</summary>
    public static void Create(DatabaseLocation database)
    {
        if (!database.CanInitialize && !database.CreateOnStartup)
        {
            throw new InvalidOperationException("The database location does not allow creating a database.");
        }

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlite(database.CreateConnectionString!)
            .Options;

        using var context = new PortalDbContext(options);
        context.Database.EnsureCreated();
    }

    /// <summary>--init-db. Idempotent: an existing database is reported and left unchanged.</summary>
    public static int Run(DatabaseLocation database, TextWriter output, TextWriter error)
    {
        output.WriteLine($"Database: {database.FilePath} (from {database.Origin})");

        switch (database.State)
        {
            case DatabaseState.Exists:
                output.WriteLine($"The database already exists ({database.SizeBytes} bytes). Nothing was changed.");
                return 0;

            case DatabaseState.Missing:
            case DatabaseState.EmptyFile:
            case DatabaseState.DirectoryMissing:
                break;

            default:
                error.WriteLine($"Cannot initialize: {database.StateText}. Nothing was changed.");
                if (database.Blocker is not null) error.WriteLine(database.Blocker);
                return 2;
        }

        var directory = Path.GetDirectoryName(database.FilePath!)!;

        if (!Directory.Exists(directory))
        {
            // Only the last directory level is created, so a mistyped path cannot build a whole tree.
            var parent = Path.GetDirectoryName(directory);
            if (parent is null || !Directory.Exists(parent))
            {
                error.WriteLine($"Cannot initialize: neither {directory} nor its parent directory exists. Create the parent directory first.");
                return 2;
            }

            Directory.CreateDirectory(directory);
            output.WriteLine($"Created directory {directory}");
        }

        try
        {
            Create(database);
        }
        catch (Exception ex)
        {
            error.WriteLine($"Cannot initialize the database: {ex.GetType().Name}: {ex.Message}");
            return 2;
        }

        output.WriteLine("Created a new empty database with the current schema.");
        return 0;
    }
}
