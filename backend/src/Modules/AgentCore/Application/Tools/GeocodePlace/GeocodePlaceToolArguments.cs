namespace AgentCore.Application.Tools.GeocodePlace;

public sealed record GeocodePlaceToolArguments(
    string Query,
    string? CountryCode = "vn"
);
