using AgentCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentCore.Infrastructure.Configurations;

public class AgentSessionConfiguration : IEntityTypeConfiguration<AgentSession>
{
    public void Configure(
        EntityTypeBuilder<AgentSession> builder
    )
    {
        builder.ToTable("AgentSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();
        builder.Property(x => x.UserId);
        builder.Property(x => x.CreatedAt)
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .IsRequired();
        builder.HasIndex(x => x.UserId);
        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Messages)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.ToolCalls)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.ToolCalls)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
