using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Users.Domain.Entities;

namespace Users.Infrastructure.Configurations;

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("Friendships");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SenderId)
            .IsRequired();

        builder.Property(x => x.ReceiverEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ReceiverId);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Token)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => x.Token);
        builder.HasIndex(x => new { x.SenderId, x.ReceiverEmail });
        builder.HasIndex(x => x.ReceiverId);

        builder.HasOne(x => x.Sender)
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Receiver)
            .WithMany()
            .HasForeignKey(x => x.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
