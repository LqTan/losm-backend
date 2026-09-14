using Search.Application.Abstractions;

namespace Search.Application.Search.Queries.SearchPlaces;

public sealed class SearchPlacesHandler
{
    private readonly IPlaceSearchService _placeSearchService;
    private readonly IRelevanceScorer _relevanceScorer;
    private readonly IRankingService _rankingService;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;

    public SearchPlacesHandler(
        IPlaceSearchService placeSearchService,
        IRelevanceScorer relevanceScorer,
        IRankingService rankingService,
        Configuration.Application.Abstractions.ITuningProvider tuning)
    {
        _placeSearchService = placeSearchService;
        _relevanceScorer = relevanceScorer;
        _rankingService = rankingService;
        _tuning = tuning;
    }

    public async Task<IReadOnlyList<SearchResult>> HandleAsync(
        SearchPlacesQuery query,
        CancellationToken cancellationToken = default)
    {
        var searchOptions = await _tuning.GetSearchOptionsAsync(cancellationToken);
        var radiusKm = query.RadiusKm > 0 ? query.RadiusKm : searchOptions.DefaultRadiusKm;

        var candidates = await _placeSearchService.SearchAsync(
            query.Query,
            query.Latitude,
            query.Longitude,
            radiusKm,
            cancellationToken
        );

        var relevanceScores = await _relevanceScorer.ScoreAsync(
            query.Query,
            candidates,
            cancellationToken
        );

        var scoreByPlaceId = relevanceScores.ToDictionary(
            x => x.PlaceId,
            x => x.Score
        );

        var results = new List<SearchResult>(candidates.Count);

        foreach (var place in candidates)
        {
            var relevanceScore = scoreByPlaceId.GetValueOrDefault(place.Id);
            var distanceKm = _rankingService.CalculateDistanceKm(
                query.Latitude,
                query.Longitude,
                place.Latitude,
                place.Longitude
            );

            var finalScore = await _rankingService.CalculateFinalScoreAsync(
                relevanceScore,
                distanceKm,
                radiusKm,
                place.Rating,
                originDistancesKm: null,
                profileName: "Default",
                ct: cancellationToken
            );

            results.Add(new SearchResult(
                place.Id,
                place.Name,
                place.Address,
                place.Latitude,
                place.Longitude,
                place.Category,
                relevanceScore,
                distanceKm,
                finalScore
            ));
        }

        return results
            .OrderByDescending(x => x.FinalScore)
            .Take(searchOptions.TopK > 0 ? searchOptions.TopK : results.Count)
            .ToList();
    }
}
