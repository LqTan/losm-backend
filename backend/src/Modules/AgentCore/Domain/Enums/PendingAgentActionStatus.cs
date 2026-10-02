namespace AgentCore.Domain.Enums;

public enum PendingAgentActionStatus
{
    Pending = 0,
    Confirmed = 1,
    Executing = 2,
    Completed = 3,
    PartiallyFailed = 4,
    Failed = 5,
    Cancelled = 6
}
