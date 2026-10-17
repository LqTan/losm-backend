using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Places.Domain.Entities;

namespace Places.Infrastructure.Configurations;

public sealed class CategoryMappingConfiguration : IEntityTypeConfiguration<CategoryMapping>
{
    public void Configure(EntityTypeBuilder<CategoryMapping> builder)
    {
        builder.ToTable("CategoryMappings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GroupName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Category)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.GroupName);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => new { x.GroupName, x.Category }).IsUnique();
    }
}
