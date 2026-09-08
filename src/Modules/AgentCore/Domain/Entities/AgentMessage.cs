using AgentCore.Domain.Enums;

namespace AgentCore.Domain.Entities;

public class AgentMessage
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public AgentMessageRole Role { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    private AgentMessage(){}
    internal AgentMessage(
        Guid id,
        Guid sessionId,
        AgentMessageRole role,
        string content
    )
    {
        Id = id;
        SessionId = sessionId;
        Role = role;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }
}
