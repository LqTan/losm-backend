namespace Search.Application.Search.Queries.SearchPlaces;

public sealed record SearchResult(
    Guid PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    string? Category,
    string? OpeningHours,
    double RelevanceScore,
    double DistanceKm,
    double FinalScore
);
