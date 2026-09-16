namespace AgentCore.Application.Agent.Queries.GetAgentSessionsByUser;

public sealed record GetAgentSessionsByUserResult(
    Guid Id,
    string Title,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
