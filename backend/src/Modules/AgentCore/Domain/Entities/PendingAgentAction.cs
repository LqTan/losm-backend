using AgentCore.Domain.Enums;

namespace AgentCore.Domain.Entities;

public sealed class PendingAgentAction
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? PlanId { get; private set; }
    public string ActionType { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public Guid? ConfirmationId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public PendingAgentActionStatus Status { get; private set; }
    public string? ResultJson { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private PendingAgentAction() { }

    public PendingAgentAction(
        Guid id,
        Guid sessionId,
        Guid userId,
        string actionType,
        string payloadJson,
        string description,
        Guid? confirmationId = null,
        string? idempotencyKey = null)
    {
        Id = id;
        SessionId = sessionId;
        UserId = userId;
        ActionType = actionType;
        PayloadJson = payloadJson;
        Description = description;
        ConfirmationId = confirmationId;
        IdempotencyKey = idempotencyKey;
        Status = PendingAgentActionStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Confirm(Guid confirmationId)
    {
        if (Status != PendingAgentActionStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Only a pending action can be confirmed. Current status: {Status}");
        }
        ConfirmationId = confirmationId;
        Status = PendingAgentActionStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void StartExecution()
    {
        if (Status != PendingAgentActionStatus.Confirmed)
        {
            throw new InvalidOperationException(
                $"Only a confirmed action can start execution. Current status: {Status}");
        }
        Status = PendingAgentActionStatus.Executing;
    }

    public void Complete(string resultJson)
    {
        if (Status != PendingAgentActionStatus.Executing)
        {
            throw new InvalidOperationException(
                $"Only an executing action can be completed. Current status: {Status}");
        }
        ResultJson = resultJson;
        Status = PendingAgentActionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void PartiallyFail(string resultJson, string error)
    {
        if (Status != PendingAgentActionStatus.Executing)
        {
            throw new InvalidOperationException(
                $"Only an executing action can partially fail. Current status: {Status}");
        }
        ResultJson = resultJson;
        Error = error;
        Status = PendingAgentActionStatus.PartiallyFailed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string error)
    {
        Status = PendingAgentActionStatus.Failed;
        Error = error;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != PendingAgentActionStatus.Pending &&
            Status != PendingAgentActionStatus.Confirmed)
        {
            throw new InvalidOperationException(
                $"Only a pending or confirmed action can be cancelled. Current status: {Status}");
        }
        Status = PendingAgentActionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }
}
