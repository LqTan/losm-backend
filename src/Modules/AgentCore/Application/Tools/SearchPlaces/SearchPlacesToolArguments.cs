using Places.Application.Abstractions;

namespace AgentCore.Application.Tools.SearchPlaces;

public sealed record SearchPlacesToolArguments(
    string Query,
    double RadiusKm = 5,
    int Limit = 10,
    string? PlaceType = null,
    string? Purpose = null,
    string? TimeOfDay = null,
    double? ReferenceLatitude = null,
    double? ReferenceLongitude = null,
    IReadOnlyList<string>? AppliedFilters = null,
    BoundingBox? Box = null
);
