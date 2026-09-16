namespace Places.Application.Abstractions;

public interface IGeocodingService
{
    Task<GeocodedPlace?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default);
}

public sealed record GeocodedPlace(
    string OsmType,
    long OsmId,
    string Name,
    string DisplayName,
    double Latitude,
    double Longitude,
    string? Category,
    string? Type,
    BoundingBox? Box = null
);
