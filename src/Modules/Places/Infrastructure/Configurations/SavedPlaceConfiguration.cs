using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Places.Domain.Entities;

namespace Places.Infrastructure.Persistence.Configurations;

public sealed class SavedPlaceConfiguration
    : IEntityTypeConfiguration<SavedPlace>
{
    public void Configure(EntityTypeBuilder<SavedPlace> builder)
    {
        builder.ToTable("SavedPlaces");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.PlaceId)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.PlaceId })
            .IsUnique();
    }
}
