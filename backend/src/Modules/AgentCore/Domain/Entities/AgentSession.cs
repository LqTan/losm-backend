using AgentCore.Domain.Enums;

namespace AgentCore.Domain.Entities;

public class AgentSession
{
    private readonly List<AgentMessage> _messages = [];
    private readonly List<AgentToolCall> _toolCalls = [];
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<AgentMessage> Messages => _messages;
    public IReadOnlyCollection<AgentToolCall> ToolCalls => _toolCalls;
    private AgentSession(){}
    public AgentSession(
        Guid id,
        Guid? userId
    )
    {
        Id = id;
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
    public AgentMessage AddMessage(
        AgentMessageRole role,
        string content
    )
    {
        var message = new AgentMessage(
            Guid.NewGuid(),
            Id,
            role,
            content
        );
        _messages.Add(message);
        UpdatedAt = DateTime.UtcNow;
        return message;
    }
    public AgentToolCall StartToolCall(
        string toolName,
        string argumentsJson
    )
    {
        var toolCall = new AgentToolCall(
            Guid.NewGuid(),
            Id,
            toolName,
            argumentsJson
        );
        _toolCalls.Add(toolCall);
        UpdatedAt = DateTime.UtcNow;
        return toolCall;
    }

    public void CompleteToolCall(
        Guid toolCallId,
        string resultJson
    )
    {
        var toolCall = _toolCalls
            .First(x => x.Id == toolCallId);
        toolCall.Complete(resultJson);
        UpdatedAt = DateTime.UtcNow;
    }

    public void FailToolCall(
        Guid toolCallId,
        string error
    )
    {
        var toolCall = _toolCalls
            .First(x => x.Id == toolCallId);
        toolCall.Fail(error);
        UpdatedAt = DateTime.UtcNow;
    }
}
