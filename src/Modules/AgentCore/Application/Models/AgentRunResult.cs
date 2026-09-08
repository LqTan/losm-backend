namespace AgentCore.Application.Models;

public sealed record AgentRunResult(
    Guid SessionId,
    string Answer
);
