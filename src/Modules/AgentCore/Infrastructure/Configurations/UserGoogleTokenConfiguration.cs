using AgentCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentCore.Infrastructure.Configurations;

public sealed class UserGoogleTokenConfiguration
    : IEntityTypeConfiguration<UserGoogleToken>
{
    public void Configure(EntityTypeBuilder<UserGoogleToken> builder)
    {
        builder.ToTable("UserGoogleTokens");

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.UserId).ValueGeneratedNever();

        builder.Property(x => x.GoogleSub)
            .HasColumnType("nvarchar(128)")
            .IsRequired();

        builder.Property(x => x.GoogleEmail)
            .HasColumnType("nvarchar(320)")
            .IsRequired();

        builder.Property(x => x.AccessToken)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.RefreshToken)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.AccessTokenExpiresAt).IsRequired();

        builder.Property(x => x.Scopes)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.GoogleSub)
            .IsUnique()
            .HasDatabaseName("UX_UserGoogleTokens_GoogleSub");
    }
}