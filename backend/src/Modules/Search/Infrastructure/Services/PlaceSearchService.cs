using Places.Application.Abstractions;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class PlaceSearchService : IPlaceSearchService
{
    private readonly IPlaceSpatialSearchService _places;

    public PlaceSearchService(IPlaceSpatialSearchService places)
    {
        _places = places;
    }

    public async Task<IReadOnlyList<PlaceCandidate>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        var hits = await _places.SearchAsync(
            query, latitude, longitude, radiusKm, candidateLimit, cancellationToken);

        return hits
            .Select(h => new PlaceCandidate(
                h.Id,
                h.Name,
                h.Address,
                h.Category,
                h.OpeningHours,
                h.Latitude,
                h.Longitude))
            .ToList();
    }

    public async Task<IReadOnlyList<PlaceCandidate>> SearchByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int candidateLimit,
        string? amenity,
        CancellationToken cancellationToken)
    {
        var centerLat = boundingBox.CenterLatitude;
        var centerLng = boundingBox.CenterLongitude;
        var radiusKm = Math.Sqrt(
            Math.Pow((boundingBox.MaxLat - boundingBox.MinLat) * 111.0, 2) +
            Math.Pow((boundingBox.MaxLng - boundingBox.MinLng) * 111.0 *
                     Math.Cos(centerLat * Math.PI / 180.0), 2));

        return await SearchAsync(
            query, centerLat, centerLng, radiusKm, candidateLimit, cancellationToken);
    }
}