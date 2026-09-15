using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(review => review.Id);
        builder.Property(review => review.PlaceId)
            .IsRequired();
        builder.Property(review => review.UserId)
            .IsRequired();
        builder.Property(review => review.Rating)
            .IsRequired();
        builder.Property(review => review.Comment)
            .HasMaxLength(1000);
        builder.Property(review => review.CreatedAt)
            .IsRequired();
        builder.Property(review => review.UpdatedAt);
        builder.HasIndex(review => review.PlaceId);
        builder.HasIndex(review => review.UserId);
        builder.HasIndex(review => new { review.UserId, review.PlaceId })
            .IsUnique()
            .HasDatabaseName("UX_Reviews_UserId_PlaceId");
    }
}