using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sandbox.Domain;

namespace Sandbox.Infrastructure.Persistence.Configurations;

public class TodoConfiguration : IEntityTypeConfiguration<Todo>
{
    public void Configure(EntityTypeBuilder<Todo> builder)
    {
        builder.ToTable("Todos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.IsCompleted)
            .IsRequired();
    }
}