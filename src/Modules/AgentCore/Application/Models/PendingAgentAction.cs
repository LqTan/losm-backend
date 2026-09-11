namespace AgentCore.Application.Models;

public enum PendingAgentActionType
{
    SavePlace = 0,
    CreateReview = 1
}

public sealed record PendingAgentAction(
    Guid Id,
    Guid SessionId,
    Guid UserId,
    PendingAgentActionType Type,
    string Description,
    Guid? PlaceId,
    string? Note,
    int? Rating,
    string? Comment,
    DateTime CreatedAt
);

public sealed record ConfirmAgentActionOutcome(
    Guid ActionId,
    PendingAgentActionType Type,
    string Status,
    string? Detail
);
