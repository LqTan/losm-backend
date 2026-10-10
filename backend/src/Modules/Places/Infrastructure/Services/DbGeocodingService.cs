using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Services;

public sealed class DbGeocodingService : IGeocodingService
{
    private readonly PlacesDbContext _db;

    public DbGeocodingService(PlacesDbContext db)
    {
        _db = db;
    }

    public async Task<GeocodeResult?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var match = await _db.Places
            .AsNoTracking()
            .Where(p => EF.Functions.Like(p.Name, $"%{query.Trim()}%"))
            .OrderBy(p => p.Name.Length)
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null) return null;

        return new GeocodeResult(
            Name: match.Name,
            DisplayName: match.Address ?? match.Name,
            Latitude: match.Latitude,
            Longitude: match.Longitude,
            Category: match.Category,
            Type: "place",
            Box: null);
    }
}