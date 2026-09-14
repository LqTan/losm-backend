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
        return places
            .Select(place => new PlaceCandidate(
                place.Id,
                place.Name,
                place.Address,
                place.Category,
                place.Latitude,
                place.Longitude,
                place.Rating
            )).ToList();
    }
}
