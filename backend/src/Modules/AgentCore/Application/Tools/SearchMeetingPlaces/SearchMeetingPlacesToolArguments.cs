namespace AgentCore.Application.Tools.SearchMeetingPlaces;

public sealed record SearchMeetingPlaceOriginArgument(
    string? Name = null,
    double? Latitude = null,
    double? Longitude = null
);

public sealed record SearchMeetingPlacesToolArguments(
    string Query,
    IReadOnlyList<SearchMeetingPlaceOriginArgument> Origins,
    double? RadiusKm = null,
    int? TopK = null
);
