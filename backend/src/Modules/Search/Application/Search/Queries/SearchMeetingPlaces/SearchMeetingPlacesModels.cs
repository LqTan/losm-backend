namespace Search.Application.Search.Queries.SearchMeetingPlaces;

public sealed record SearchMeetingPlaceOrigin(
    string? Name,
    double Latitude,
    double Longitude
);

public sealed record SearchMeetingPlacesQuery(
    string Query,
    IReadOnlyList<SearchMeetingPlaceOrigin> Origins,
    double? RadiusKm = null,
    int? TopK = null,
    string? TargetCategory = null,
    string? SpecificPreference = null
);

public sealed record SearchMeetingPlaceResult(
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
    IReadOnlyList<double>? DurationMinutesByOrigin = null,
    double? AverageDurationMinutes = null,
    double? SpreadMinutes = null,
    bool IsRoadRouting = false,
    bool IsOnnxScored = false
);

public sealed record SearchMeetingPlacesResult(
    IReadOnlyList<SearchMeetingPlaceResult> Results,
    double CentroidLatitude,
    double CentroidLongitude,
    double SearchRadiusKm,
    string Profile,
    string RankingMethod = "system"
);
