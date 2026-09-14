namespace Places.Application.Places.Queries.SearchPlaces;

public sealed record SearchPlacesQuery(
    string Query,
    double Latitude,
    double Longitude,
    double RadiusKm,
    int CandidateLimit
);
