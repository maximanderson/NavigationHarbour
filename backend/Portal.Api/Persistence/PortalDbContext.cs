using Microsoft.EntityFrameworkCore;
using Portal.Api.Models;

namespace Portal.Api.Persistence;

public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options)
        : base(options)
    {
    }

    public DbSet<UsageLog> UsageLogs => Set<UsageLog>();
}
