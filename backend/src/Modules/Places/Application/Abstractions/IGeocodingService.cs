namespace Places.Application.Abstractions;

public sealed record GeocodeResult(
    string Name,
    string DisplayName,
    double Latitude,
    double Longitude,
    string? Category,
    string? Type,
    BoundingBox? Box);

public interface IGeocodingService
{
    Task<GeocodeResult?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default);
}