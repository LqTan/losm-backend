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
    int? TopK = null
);

public sealed record SearchMeetingPlaceResult(
    Guid PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    string? Category,
    IReadOnlyList<double> DistanceKmByOrigin,
    double AverageDistanceKm,
    double MaxDistanceKm,
    double SpreadKm,
    double RelevanceScore,
    double DistanceScore,
    double FairnessScore,
    double RatingScore,
    double FinalScore
);

public sealed record SearchMeetingPlacesResult(
    IReadOnlyList<SearchMeetingPlaceResult> Results,
    double CentroidLatitude,
    double CentroidLongitude,
    double SearchRadiusKm,
    string Profile
);
