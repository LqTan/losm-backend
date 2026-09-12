using AgentCore.Application.Models;

namespace AgentCore.Application.Models;

public sealed record AgentRunResult(
    Guid SessionId,
    string Answer,
    IReadOnlyList<AgentActivityStep> Steps,
    IReadOnlyList<PendingAgentAction> PendingActions,
    IReadOnlyList<AttachedPlace> AttachedPlaces
);
