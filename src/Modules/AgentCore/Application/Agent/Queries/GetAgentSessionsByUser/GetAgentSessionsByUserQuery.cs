namespace AgentCore.Application.Agent.Queries.GetAgentSessionsByUser;

public sealed record GetAgentSessionsByUserQuery(
    Guid UserId,
    int Limit = 50
);
