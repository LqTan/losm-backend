namespace AgentCore.Application.Agent.Queries.GetPendingActionById;

public sealed record GetPendingActionByIdResult(
    Guid ActionId,
    Guid SessionId,
    string ActionType,
    string Status,
    string Description,
    Guid? ConfirmationId,
    string PayloadJson,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt,
    string? Error
);
