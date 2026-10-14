namespace Search.Application.Abstractions;

public sealed record OsrmRouteResult(
    double DistanceKm,
    double DurationMinutes,
    IReadOnlyList<GeoCoordinate> Coordinates,
    string? Summary
);

public interface IOsrmRouteService
{
    Task<OsrmRouteResult?> GetRouteAsync(
        GeoCoordinate origin,
        GeoCoordinate destination,
        CancellationToken cancellationToken = default
    );
}
