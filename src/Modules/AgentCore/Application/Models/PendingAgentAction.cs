using AgentCore.Domain.Entities;

namespace AgentCore.Application.Models;

public sealed record ConfirmAgentActionOutcome(
    Guid ActionId,
    string ActionType,
    string Status,
    string? Detail
)
{
    public static ConfirmAgentActionOutcome FromEntity(
        PendingAgentAction action, string status, string? detail) =>
        new(action.Id, action.ActionType, status, detail);
}
