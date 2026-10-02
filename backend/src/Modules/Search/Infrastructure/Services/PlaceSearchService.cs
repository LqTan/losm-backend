using Places.Application.Abstractions;
using Places.Application.Contracts;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class PlaceSearchService : IPlaceSearchService
{
    private readonly IPlacesSearchContract _places;
    public PlaceSearchService(IPlacesSearchContract places)
    {
        _places = places;
    }
    public async Task<IReadOnlyList<PlaceCandidate>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        CancellationToken cancellationToken
    )
    {
        var places = await _places.SearchAsync(
            query,
            latitude,
            longitude,
            radiusKm,
            candidateLimit,
            cancellationToken
        );
        return MapToCandidates(places);
    }

    public async Task<IReadOnlyList<PlaceCandidate>> SearchByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int candidateLimit,
        string? amenity,
        CancellationToken cancellationToken
    )
    {
        var places = await _places.SearchByBoundingBoxAsync(
            query,
            boundingBox,
            candidateLimit,
            amenity,
            cancellationToken
        );
        return MapToCandidates(places);
    }

    private static IReadOnlyList<PlaceCandidate> MapToCandidates(IReadOnlyList<Places.Domain.Entities.Place> places)
    {
        return places
            .Select(place => new PlaceCandidate(
                place.Id,
                place.Name,
                place.Address,
                place.Category,
                place.OpeningHours,
                place.Latitude,
                place.Longitude
            ))
            .ToList();
    }
}
