using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Places.Domain.Entities;

namespace Places.Infrastructure.Configurations;

public sealed class CategoryOverrideConfiguration : IEntityTypeConfiguration<CategoryOverride>
{
    public void Configure(EntityTypeBuilder<CategoryOverride> builder)
    {
        builder.ToTable("CategoryOverrides");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Keyword)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.TargetCategory)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.Keyword).IsUnique();
    }
}
