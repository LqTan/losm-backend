using AgentCore.Application.Models;
using AgentCore.Domain.Entities;

namespace AgentCore.Application.Agent.Commands.ExecuteAgent;

public sealed record ExecuteAgentResult(
    Guid SessionId,
    string Answer,
    IReadOnlyList<AgentActivityStep> Steps,
    IReadOnlyList<PendingAgentAction> PendingActions,
    IReadOnlyList<AttachedPlace> AttachedPlaces
);
