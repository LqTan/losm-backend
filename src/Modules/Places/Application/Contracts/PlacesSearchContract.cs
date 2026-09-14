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
            query,
            latitude,
            longitude,
            radiusKm,
            candidateLimit
        );
        return await _handler.HandleAsync(
            searchQuery,
            cancellationToken
        );
    }
}
