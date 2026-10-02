using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain.Entities;

namespace Users.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id);
        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.Email)
            .HasMaxLength(255)            
            .IsRequired();            
        builder.HasIndex(x => x.Email)
            .IsUnique();
        builder.Property(x => x.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}