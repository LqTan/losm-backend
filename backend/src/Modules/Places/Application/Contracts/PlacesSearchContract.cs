using Places.Application.Abstractions;
using Places.Application.Places.Queries.SearchPlaces;
using Places.Domain.Entities;

namespace Places.Application.Contracts;

public sealed class PlacesSearchContract : IPlacesSearchContract
{
    private readonly SearchPlacesHandler _handler;

    public PlacesSearchContract(SearchPlacesHandler handler)
    {
        _handler = handler;
    }
    public async Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        CancellationToken cancellationToken
    )
    {
        var searchQuery = new SearchPlacesQuery(
            Query: query,
            Latitude: latitude,
            Longitude: longitude,
            RadiusKm: radiusKm,
            CandidateLimit: candidateLimit,
            Box: null,
            Amenity: null
        );
        return await _handler.HandleAsync(
            searchQuery,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<Place>> SearchByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int candidateLimit,
        string? amenity,
        CancellationToken cancellationToken
    )
    {
        var searchQuery = new SearchPlacesQuery(
            Query: query,
            Latitude: null,
            Longitude: null,
            RadiusKm: 0,
            CandidateLimit: candidateLimit,
            Box: boundingBox,
            Amenity: amenity
        );
        return await _handler.HandleAsync(
            searchQuery,
            cancellationToken
        );
    }
}
