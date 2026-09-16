using Places.Application.Abstractions;

namespace Search.Application.Search.Queries.SearchPlaces;

public sealed record SearchPlacesQuery(
    string Query,
    double? Latitude,
    double? Longitude,
    double? RadiusKm = null,
    int? CandidateLimit = null,
    BoundingBox? Box = null,
    string? Amenity = null
);
