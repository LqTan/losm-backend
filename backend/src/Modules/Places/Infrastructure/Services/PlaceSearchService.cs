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
        IReadOnlyList<string>? categories = null,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 20;
        if (radiusKm <= 0) radiusKm = 5;

        var latDelta = radiusKm / 111.0;
        var cosLat = Math.Cos(latitude * Math.PI / 180.0);
        var lngDelta = Math.Max(0.001, radiusKm / (111.0 * cosLat));

        var clean = (query ?? "").Trim();

        // 1. Basic geographic bounding box (filtering out unsafe places)
        var baseQuery = _db.Places.AsNoTracking().Where(p =>
            !p.IsNotSafe &&
            p.Latitude >= latitude - latDelta &&
            p.Latitude <= latitude + latDelta &&
            p.Longitude >= longitude - lngDelta &&
            p.Longitude <= longitude + lngDelta);

        // 2. Search by category (if specified) and/or keywords
        if (categories is { Count: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(clean))
            {
                var pattern = $"%{clean}%";
                baseQuery = baseQuery.Where(p =>
                    (p.Category != null && categories.Contains(p.Category)) ||
                    EF.Functions.Like(p.Name, pattern));
            }
            else
            {
                baseQuery = baseQuery.Where(p =>
                    p.Category != null && categories.Contains(p.Category));
            }
        }
        else if (!string.IsNullOrWhiteSpace(clean))
        {
            var pattern = $"%{clean}%";
            baseQuery = baseQuery.Where(p =>
                EF.Functions.Like(p.Name, pattern) ||
                (p.Category != null && EF.Functions.Like(p.Category, pattern)) ||
                (p.Address != null && EF.Functions.Like(p.Address, pattern)));
        }

        var cosLatSq = cosLat * cosLat;

        var candidates = await baseQuery
            .OrderBy(p => (p.Latitude - latitude) * (p.Latitude - latitude) + (p.Longitude - longitude) * (p.Longitude - longitude) * cosLatSq)
            .Take(Math.Min(limit * 10, 1000))
            .ToListAsync(cancellationToken);

        // Fallback: If no places match keyword within radius,
        // retrieve safe places in radius ordered by distance for BM25 / Ranking evaluation
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(clean))
        {
            candidates = await _db.Places.AsNoTracking().Where(p =>
                !p.IsNotSafe &&
                p.Latitude >= latitude - latDelta &&
                p.Latitude <= latitude + latDelta &&
                p.Longitude >= longitude - lngDelta &&
                p.Longitude <= longitude + lngDelta)
                .OrderBy(p => (p.Latitude - latitude) * (p.Latitude - latitude) + (p.Longitude - longitude) * (p.Longitude - longitude) * cosLatSq)
                .Take(Math.Min(limit * 10, 1000))
                .ToListAsync(cancellationToken);
        }

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