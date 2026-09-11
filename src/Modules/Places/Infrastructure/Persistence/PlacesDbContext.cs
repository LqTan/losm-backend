using Microsoft.EntityFrameworkCore;
using Places.Domain.Entities;

namespace Places.Infrastructure.Persistence;

public sealed class PlacesDbContext : DbContext
{
    public PlacesDbContext(DbContextOptions<PlacesDbContext> options)
        : base(options){}

    public DbSet<Place> Places => Set<Place>();
    public DbSet<SavedPlace> SavedPlaces => Set<SavedPlace>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PlacesDbContext).Assembly
        );
    }
}
