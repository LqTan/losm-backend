using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;
using Places.Domain.Entities;

namespace Places.Application.Places.Queries.SearchPlaces;

public sealed class SearchPlacesHandler
{
    private readonly IPlaceProvider _placeProvider;
    private readonly IPlaceRepository _placeRepository;
    private readonly ILogger<SearchPlacesHandler> _logger;

    public SearchPlacesHandler(
        IPlaceProvider placeProvider,
        IPlaceRepository placeRepository,
        ILogger<SearchPlacesHandler> logger
    )
    {
        _placeProvider = placeProvider;
        _placeRepository = placeRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Place>> HandleAsync(
        SearchPlacesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var places = await _placeProvider.SearchAsync(
            query.Query,
            query.Latitude,
            query.Longitude,
            query.RadiusKm,
            cancellationToken
        );

        if (places.Count == 0)
        {
            return [];
        }

        try
        {
            return await _placeRepository.UpsertRangeAsync(
                places,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upsert {Count} places; returning provider results without persisting",
                places.Count);
            return places;
        }
    }
}
