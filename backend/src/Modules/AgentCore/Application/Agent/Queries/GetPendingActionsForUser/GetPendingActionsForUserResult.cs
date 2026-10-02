namespace AgentCore.Application.Agent.Queries.GetPendingActionsForUser;

public sealed record GetPendingActionsForUserResult(
    Guid ActionId,
    Guid SessionId,
    string ActionType,
    string Status,
    string Description,
    string PayloadJson,
    DateTime CreatedAt
);