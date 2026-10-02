using AgentCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentCore.Infrastructure.Configurations;

public sealed class LastSearchContextConfiguration
    : IEntityTypeConfiguration<LastSearchContext>
{
    public void Configure(EntityTypeBuilder<LastSearchContext> builder)
    {
        builder.ToTable("LastSearchContexts");

        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.SessionId).ValueGeneratedNever();

        builder.Property(x => x.Query)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.CenterLatitude).IsRequired();
        builder.Property(x => x.CenterLongitude).IsRequired();
        builder.Property(x => x.RadiusKm).IsRequired();

        builder.Property(x => x.ResultPlaceIdsJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.FiltersJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.AttachedPlacesJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.UpdatedAt);
    }
}
