using AgentCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentCore.Infrastructure.Configurations;

public class AgentToolCallConfiguration : IEntityTypeConfiguration<AgentToolCall>
{
    public void Configure(
        EntityTypeBuilder<AgentToolCall> builder
    )
    {
        builder.ToTable("AgentToolCalls");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();
        builder.Property(x => x.SessionId)
            .IsRequired();
        builder.Property(x => x.ToolName)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.ArgumentsJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        builder.Property(x => x.ResultJson)
            .HasColumnType("nvarchar(max)");
        builder.Property(x => x.Error)
            .HasMaxLength(4000);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .IsRequired();
        builder.Property(x => x.CompletedAt);
        builder.HasIndex(x => new
        {
            x.SessionId,
            x.CreatedAt
        });
    }
}
