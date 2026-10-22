using AgentCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentCore.Infrastructure.Configurations;

public sealed class PromptRevisionConfiguration : IEntityTypeConfiguration<PromptRevision>
{
    public void Configure(EntityTypeBuilder<PromptRevision> builder)
    {
        builder.ToTable("AgentPromptRevisions");
        builder.HasKey(p => new { p.Key, p.Version });
        builder.Property(p => p.Key).HasMaxLength(150);
        builder.Property(p => p.Content).IsRequired();
    }
}
