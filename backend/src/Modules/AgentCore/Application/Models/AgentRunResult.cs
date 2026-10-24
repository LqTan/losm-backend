using AgentCore.Domain.Entities;

namespace AgentCore.Application.Models;

public sealed record AgentRunResult(
    Guid SessionId,
    string Answer,
    IReadOnlyList<AgentActivityStep> Steps,
    IReadOnlyList<PendingAgentAction> PendingActions,
    IReadOnlyList<AttachedPlace> AttachedPlaces,
    AttachedRoute? AttachedRoute = null,
    IReadOnlyList<Transit.Application.Network.Queries.TransitQueryResult>? TransitResults = null,
    AgentMeetingResults? MeetingResults = null
);
