using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;
using Places.Domain.Entities;

namespace Places.Application.Places.Queries.SearchPlaces;

public sealed class SearchPlacesHandler
{
    private readonly IPlaceSpatialSearchService _search;
    private readonly IPlaceRepository _placeRepository;
    private readonly ILogger<SearchPlacesHandler> _logger;

    public SearchPlacesHandler(
        IPlaceSpatialSearchService search,
        IPlaceRepository placeRepository,
        ILogger<SearchPlacesHandler> logger)
    {
        _search = search;
        _placeRepository = placeRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Place>> HandleAsync(
        SearchPlacesQuery query,
        CancellationToken cancellationToken = default)
    {
        var latitude = query.Latitude ?? 0.0;
        var longitude = query.Longitude ?? 0.0;

        try
        {
            var hits = await _search.SearchAsync(
                query.Query,
                latitude,
                longitude,
                query.RadiusKm,
                query.CandidateLimit,
                cancellationToken);

            if (hits.Count == 0) return [];

            var ids = hits.Select(h => h.Id).ToHashSet();
            var places = new List<Place>();
            foreach (var id in ids)
            {
                var place = await _placeRepository.GetByIdAsync(id, cancellationToken);
                if (place is not null) places.Add(place);
            }

            var orderedIds = hits.Select(h => h.Id).ToList();
            places = places
                .OrderBy(p => orderedIds.IndexOf(p.Id))
                .ToList();

            return places;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Place search failed for query={Query} at ({Lat},{Lon})",
                query.Query, latitude, longitude);
            return [];
        }
    }
}