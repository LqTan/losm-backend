using AgentCore.Application.Models;

namespace AgentCore.Application.Agent.Commands.ExecuteAgent;

public sealed record ExecuteAgentResult(
    Guid SessionId,
    string Answer,
    IReadOnlyList<AgentActivityStep> Steps,
    IReadOnlyList<PendingAgentAction> PendingActions
);
