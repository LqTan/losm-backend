namespace Agent.Domain;

public sealed class ConversationTurn
{
    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}