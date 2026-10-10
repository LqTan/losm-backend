using Microsoft.EntityFrameworkCore;
using Overture.Domain.Entities;

namespace Overture.Infrastructure.Persistence;

public sealed class OvertureDbContext : DbContext
{
    public OvertureDbContext(DbContextOptions<OvertureDbContext> options)
        : base(options) { }

    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OvertureDbContext).Assembly);
    }
}