namespace AgentCore.Application.Agent.Queries.GetAgentSessionHistory;

public sealed record GetAgentSessionHistoryResult(
    Guid SessionId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AgentMessageHistoryResult> Messages,
    IReadOnlyList<AgentToolCallHistoryResult> ToolCalls,
    LastSearchContextSnapshot? LastSearchContext
);

public sealed record AgentMessageHistoryResult(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt
);

public sealed record AgentToolCallHistoryResult(
    Guid Id,
    string ToolName,
    string ArgumentsJson,
    string? ResultJson,
    string? Error,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public sealed record LastSearchContextSnapshot(
    string Query,
    double CenterLatitude,
    double CenterLongitude,
    double RadiusKm,
    IReadOnlyList<Guid> ResultPlaceIds,
    string FiltersJson,
    IReadOnlyList<LastSearchAttachedPlace> AttachedPlaces,
    DateTime UpdatedAt
);

public sealed record LastSearchAttachedPlace(
    Guid PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    string? Category,
    string? OpeningHours,
    double? DistanceKm,
    double? FinalScore
);
