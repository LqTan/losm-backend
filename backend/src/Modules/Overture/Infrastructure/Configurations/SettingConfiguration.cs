using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Overture.Domain.Entities;

namespace Overture.Infrastructure.Configurations;

public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ValueJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.Key).IsUnique();
    }
}