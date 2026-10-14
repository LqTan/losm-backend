namespace AgentCore.Presentation.Contracts;

public sealed record MeetingSearchPromptRequest(
    string Prompt,
    double? UserLatitude = null,
    double? UserLongitude = null,
    IReadOnlyList<MeetingFriendInput>? Friends = null
);

public sealed record MeetingFriendInput(
    string Name,
    double Latitude,
    double Longitude,
    string? AvatarUrl = null
);

public sealed record MeetingSearchPromptResponse(
    string Query,
    MeetingSearchCentroid? Centroid,
    IReadOnlyList<MeetingSearchOriginItem> Origins,
    IReadOnlyList<MeetingPlaceResultDto> Results,
    string? AiInsight,
    IReadOnlyList<string>? SupportedAreas = null,
    AgentCore.Application.Models.AttachedRoute? AttachedRoute = null
);

public sealed record MeetingSearchCentroid(
    double Lat,
    double Lng
);

public sealed record MeetingSearchOriginItem(
    string Id,
    string Name,
    string Address,
    double Lat,
    double Lng,
    string? AvatarUrl = null,
    bool IsMe = false
);

public sealed record MeetingPlaceResultDto(
    string PlaceId,
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
    bool IsRoadRouting
);
