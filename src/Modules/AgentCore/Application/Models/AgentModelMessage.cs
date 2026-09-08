using AgentCore.Application.Enums;

namespace AgentCore.Application.Models;

public sealed record AgentModelMessage(
    AgentModelRole Role,
    string? Content,
    IReadOnlyList<AgentModelToolCall>? ToolCalls = null,
    string? ToolCallId = null
);
