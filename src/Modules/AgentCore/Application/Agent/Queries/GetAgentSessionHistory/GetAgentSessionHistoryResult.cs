namespace AgentCore.Application.Agent.Queries.GetAgentSessionHistory;

public sealed record GetAgentSessionHistoryResult(
    Guid SessionId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AgentMessageHistoryResult> Messages,
    IReadOnlyList<AgentToolCallHistoryResult> ToolCalls
);

public sealed record AgentMessageHistoryResult(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt
);

public sealed record AgentToolCallHistoryResult(
    Guid Id,
    string ToolName,
    string ArgumentsJson,
    string? ResultJson,
    string? Error,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
