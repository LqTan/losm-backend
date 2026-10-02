namespace AgentCore.Application.Models;

public sealed record AttachedPlace(
    Guid PlaceId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    string? Category,
    double? DistanceKm,
    double? FinalScore
);
