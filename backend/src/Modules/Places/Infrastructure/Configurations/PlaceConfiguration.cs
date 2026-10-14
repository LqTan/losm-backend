using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Places.Domain.Entities;

namespace Places.Infrastructure.Configurations;

public sealed class PlaceConfiguration : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> builder)
    {
        builder.ToTable("Places");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Address)
            .HasMaxLength(500);

        builder.Property(x => x.Source)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.IsNotSafe)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.NotSafeReason)
            .HasMaxLength(500);

        builder.HasIndex(x => x.IsNotSafe);
        builder.HasIndex(x => x.Source);
        builder.HasIndex(x => x.ExternalId);
        builder.HasIndex(x => new { x.Latitude, x.Longitude });
    }
}