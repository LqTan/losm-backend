using AgentCore.Domain.Enums;

namespace AgentCore.Domain.Entities;

public class AgentToolCall
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string ToolName { get; private set; } = null!;
    public string ArgumentsJson { get; private set; } = null!;
    public string? ResultJson { get; private set; }
    public string? Error { get; private set; }
    public AgentToolCallStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AgentToolCall(){}

    internal AgentToolCall(
        Guid id,
        Guid sessionId,
        string toolName,
        string argumentsJson
    )
    {
        Id = id;
        SessionId = sessionId;
        ToolName = toolName;
        ArgumentsJson = argumentsJson;
        Status = AgentToolCallStatus.Running;
        CreatedAt = DateTime.UtcNow;
    }

    internal void Complete(string resultJson)
    {
        ResultJson = resultJson;
        Status = AgentToolCallStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    internal void Fail(string error)
    {
        Error = error;
        Status = AgentToolCallStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }
}
