namespace AgentCore.Application.Agent.Queries.GetAgentSessionHistory;

public sealed record GetAgentSessionHistoryQuery(
    Guid SessionId,
    Guid UserId
);
