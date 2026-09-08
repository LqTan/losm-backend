namespace Agent.Domain;

public sealed class ValidationDecision
{
    public bool IsAllowed { get; init; }

    public bool NeedsHumanApproval { get; init; }

    public string? Reason { get; init; }

    public static ValidationDecision Allow()
    {
        return new ValidationDecision
        {
            IsAllowed = true
        };
    }

    public static ValidationDecision Reject(string reason)
    {
        return new ValidationDecision
        {
            IsAllowed = false,
            Reason = reason
        };
    }

    public static ValidationDecision RequireHumanApproval(string reason)
    {
        return new ValidationDecision
        {
            IsAllowed = false,
            NeedsHumanApproval = true,
            Reason = reason
        };
    }
}