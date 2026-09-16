using AgentCore.Domain.Enums;

namespace AgentCore.Application.Agent.Queries.GetPendingActionsForUser;

public sealed record GetPendingActionsForUserQuery(
    Guid UserId,
    int? Limit = null
);