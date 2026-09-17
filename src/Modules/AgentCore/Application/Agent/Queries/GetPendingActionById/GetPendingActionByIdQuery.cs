namespace AgentCore.Application.Agent.Queries.GetPendingActionById;

public sealed record GetPendingActionByIdQuery(
    Guid ActionId,
    Guid UserId
);
