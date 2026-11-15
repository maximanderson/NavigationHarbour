using Microsoft.EntityFrameworkCore;
using PortalCore.Api.Models;

namespace PortalCore.Api.Persistence;

public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options)
        : base(options)
    {
    }

    public DbSet<UsageLog> UsageLogs => Set<UsageLog>();
}
