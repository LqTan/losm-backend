using Microsoft.EntityFrameworkCore;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

public sealed class ReviewsDbContext : DbContext
{
    public ReviewsDbContext(DbContextOptions<ReviewsDbContext> options)
        : base(options){}
    public DbSet<Review> Reviews => Set<Review>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ReviewsDbContext).Assembly
        );
    }
}