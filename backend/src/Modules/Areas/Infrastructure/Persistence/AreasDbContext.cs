using Areas.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Areas.Infrastructure.Persistence;

public sealed class AreasDbContext : DbContext
{
    public AreasDbContext(DbContextOptions<AreasDbContext> options)
        : base(options) { }

    public DbSet<Area> Areas => Set<Area>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AreasDbContext).Assembly
        );
    }
}