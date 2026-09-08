namespace AgentCore.Application.Agent.Commands.ExecuteAgent;

public sealed record ExecuteAgentResult(
    Guid SessionId,
    string Answer
);
