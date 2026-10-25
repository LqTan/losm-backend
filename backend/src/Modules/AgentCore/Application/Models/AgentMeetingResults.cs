namespace AgentCore.Application.Models;

public sealed record AgentMeetingCentroid(
    double Latitude,
    double Longitude
);

public sealed record AgentMeetingOrigin(
    string? Name,
    double Latitude,
    double Longitude,
    string? DisplayName
);

public sealed record AgentMeetingPlace(
    Guid PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    string? Category,
    string? OpeningHours,
    IReadOnlyList<double> DistanceKmByOrigin,
    double AverageDistanceKm,
    double MaxDistanceKm,
    double SpreadKm,
    double RelevanceScore,
    double DistanceScore,
    double FairnessScore,
    double FinalScore,
    IReadOnlyList<double>? DurationMinutesByOrigin,
    double? AverageDurationMinutes,
    double? SpreadMinutes,
    bool IsRoadRouting,
    bool IsOnnxScored = false
);

public sealed record AgentMeetingResults(
    string Query,
    AgentMeetingCentroid? Centroid,
    double SearchRadiusKm,
    string? Profile,
    IReadOnlyList<AgentMeetingOrigin> Origins,
    IReadOnlyList<AgentMeetingPlace> Results,
    string? RankingMethod = null
);
