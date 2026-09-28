using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain.Entities;
using Users.Domain.Enums;

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
        builder.Property(x => x.FullName)
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(x => x.Phone)
            .HasMaxLength(20);
        builder.Property(x => x.Gender)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.NotifyOnAccountCreation)
            .IsRequired();
        builder.Property(x => x.LastLoginAt);
        builder.Property(x => x.CreatedAt)
            .IsRequired();
        builder.Property(x => x.ChangedAt)
            .IsRequired();

        builder.HasIndex(x => x.Username);
        builder.HasIndex(x => x.Phone);
        builder.HasIndex(x => x.Role);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Role, x.CreatedAt });
    }
}
