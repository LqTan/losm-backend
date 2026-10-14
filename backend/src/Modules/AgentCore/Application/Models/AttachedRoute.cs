namespace AgentCore.Application.Models;

public sealed record AttachedRoute(
    string OriginName,
    double OriginLatitude,
    double OriginLongitude,
    string DestinationName,
    double DestinationLatitude,
    double DestinationLongitude,
    string? MemberName,
    double DistanceKm,
    double DurationMinutes,
    string? Summary,
    IReadOnlyList<IReadOnlyList<double>> Coordinates // [[lat, lng], [lat, lng], ...]
);
