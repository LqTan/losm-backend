using Areas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Areas.Infrastructure.Configurations;

public sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Level)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ExternalCode)
            .HasMaxLength(50);

        builder.Property(x => x.BboxMinLat).IsRequired();
        builder.Property(x => x.BboxMinLng).IsRequired();
        builder.Property(x => x.BboxMaxLat).IsRequired();
        builder.Property(x => x.BboxMaxLng).IsRequired();

        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => new { x.Level, x.ParentId });
        builder.HasIndex(x => x.ExternalCode);
        builder.HasIndex(x => x.Name);

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}