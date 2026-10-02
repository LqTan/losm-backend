using AgentCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentCore.Infrastructure.Configurations;

public class AgentMessageConfiguration : IEntityTypeConfiguration<AgentMessage>
{
    public void Configure(
        EntityTypeBuilder<AgentMessage> builder
    )
    {
        builder.ToTable("AgentMessages");
        builder.HasKey(x => x.Id);        
        builder.Property(x => x.Id)
            .ValueGeneratedNever();
        builder.Property(x => x.SessionId)
            .IsRequired();
        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.Content)
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .IsRequired();
        builder.HasIndex(x => new
        {
            x.SessionId,
            x.CreatedAt
        });
    }
}
