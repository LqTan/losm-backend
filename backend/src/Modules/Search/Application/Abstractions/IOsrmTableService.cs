namespace Search.Application.Abstractions;

public sealed record GeoCoordinate(double Latitude, double Longitude);

public sealed record OsrmTableMatrix(
    IReadOnlyList<IReadOnlyList<double>> DistancesKm,
    IReadOnlyList<IReadOnlyList<double>> DurationsMinutes
);

public interface IOsrmTableService
{
    Task<OsrmTableMatrix?> GetTableAsync(
        IReadOnlyList<GeoCoordinate> sources,
        IReadOnlyList<GeoCoordinate> destinations,
        CancellationToken cancellationToken = default
    );
}
