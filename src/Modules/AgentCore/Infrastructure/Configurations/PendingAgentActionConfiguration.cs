using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentCore.Infrastructure.Configurations;

public sealed class PendingAgentActionConfiguration
    : IEntityTypeConfiguration<PendingAgentAction>
{
    public void Configure(EntityTypeBuilder<PendingAgentAction> builder)
    {
        builder.ToTable("PendingAgentActions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SessionId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();

        builder.Property(x => x.ActionType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.ConfirmationId);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Error).HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ConfirmedAt);
        builder.Property(x => x.CompletedAt);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => new { x.UserId, x.Status });
        builder.HasIndex(x => x.ConfirmationId).IsUnique(false);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique(false);
    }
}
