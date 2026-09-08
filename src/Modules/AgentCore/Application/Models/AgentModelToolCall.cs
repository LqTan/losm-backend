namespace AgentCore.Application.Models;

public sealed record AgentModelToolCall(
    string Id,
    string Name,
    string ArgumentsJson
);
