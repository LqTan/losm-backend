namespace AgentCore.Application.Tools.SearchPlaces;

public sealed record SearchPlacesToolArguments(
    string Query,
    double RadiusKm = 5,
    int Limit = 10
);
