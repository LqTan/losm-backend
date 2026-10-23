using Microsoft.EntityFrameworkCore;
namespace Transit.Infrastructure;

public sealed class TransitDbContext(DbContextOptions<TransitDbContext> options) : DbContext(options)
{
    public DbSet<TransitDataset> Datasets => Set<TransitDataset>();
    public DbSet<TransitImportBatch> Batches => Set<TransitImportBatch>();
    public DbSet<TransitImportError> Errors => Set<TransitImportError>();
    public DbSet<TransitRoute> Routes => Set<TransitRoute>();
    public DbSet<TransitRoutePattern> Patterns => Set<TransitRoutePattern>();
    public DbSet<TransitStop> Stops => Set<TransitStop>();
    public DbSet<TransitRouteStop> RouteStops => Set<TransitRouteStop>();
    public DbSet<TransitServiceWindow> ServiceWindows => Set<TransitServiceWindow>();
    public DbSet<TransitFareRule> FareRules => Set<TransitFareRule>();
    public DbSet<TransitStopSafety> StopSafety => Set<TransitStopSafety>();
    public DbSet<TransitActivation> Activations => Set<TransitActivation>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => TransitSchemaV1.Configure(modelBuilder);
}
