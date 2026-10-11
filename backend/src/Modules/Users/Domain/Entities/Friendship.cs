namespace Users.Domain.Entities;

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}

public class Friendship
{
    public Guid Id { get; private set; }
    public Guid SenderId { get; private set; }
    public string ReceiverEmail { get; private set; } = null!;
    public Guid? ReceiverId { get; private set; }
    public FriendshipStatus Status { get; private set; }
    public string Token { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual User Sender { get; private set; } = null!;
    public virtual User? Receiver { get; private set; }

    private Friendship() {}

    public Friendship(Guid id, Guid senderId, string receiverEmail, Guid? receiverId, string token)
    {
        Id = id;
        SenderId = senderId;
        ReceiverEmail = receiverEmail.Trim().ToLowerInvariant();
        ReceiverId = receiverId;
        Status = FriendshipStatus.Pending;
        Token = token;
        CreatedAt = DateTime.UtcNow;
    }

    public void Accept(Guid receiverId)
    {
        ReceiverId = receiverId;
        Status = FriendshipStatus.Accepted;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Decline(Guid receiverId)
    {
        ReceiverId = receiverId;
        Status = FriendshipStatus.Declined;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkReceiver(Guid receiverId)
    {
        ReceiverId = receiverId;
    }
}
