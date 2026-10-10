using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Services;

public sealed class PlaceSpatialSearchService : IPlaceSpatialSearchService
{
    private readonly PlacesDbContext _db;

    public PlaceSpatialSearchService(PlacesDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlaceSearchHit>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 20;
        if (radiusKm <= 0) radiusKm = 5;

        var latDelta = radiusKm / 111.0;
        var cosLat = Math.Cos(latitude * Math.PI / 180.0);
        var lngDelta = Math.Max(0.001, radiusKm / (111.0 * cosLat));

        var pattern = $"%{(query ?? "").Trim()}%";
        var hasQuery = !string.IsNullOrWhiteSpace(query);

        var baseQuery = _db.Places.AsNoTracking().Where(p =>
            p.Latitude >= latitude - latDelta &&
            p.Latitude <= latitude + latDelta &&
            p.Longitude >= longitude - lngDelta &&
            p.Longitude <= longitude + lngDelta);

        if (hasQuery)
        {
            baseQuery = baseQuery.Where(p =>
                EF.Functions.Like(p.Name, pattern) ||
                (p.Address != null && EF.Functions.Like(p.Address, pattern)) ||
                (p.Category != null && EF.Functions.Like(p.Category, pattern)));
        }

        var candidates = await baseQuery
            .Take(Math.Min(limit * 10, 1000))
            .ToListAsync(cancellationToken);

        var radiusMeters = radiusKm * 1000.0;
        var hits = candidates
            .Select(p =>
            {
                var dLat = (p.Latitude - latitude) * 111000.0;
                var dLng = (p.Longitude - longitude) * 111000.0 * cosLat;
                var distance = Math.Sqrt(dLat * dLat + dLng * dLng);
                return new PlaceSearchHit(
                    p.Id, p.Name, p.Address, p.Category, p.OpeningHours,
                    p.Latitude, p.Longitude, distance);
            })
            .Where(h => h.DistanceMeters <= radiusMeters)
            .OrderBy(h => h.DistanceMeters)
            .Take(limit)
            .ToList();

        return hits;
    }
}