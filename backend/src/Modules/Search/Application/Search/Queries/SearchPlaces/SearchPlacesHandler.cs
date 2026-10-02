using Places.Application.Abstractions;
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
        if (string.IsNullOrWhiteSpace(query.Query))
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        if (query.Box is null &&
            (query.Latitude is null || query.Longitude is null))
        {
            throw new ArgumentException(
                "Either (Latitude, Longitude) or Box must be provided.",
                nameof(query));
        }

        var searchOptions = await _tuning.GetSearchOptionsAsync(cancellationToken);

        var radiusKm = query.RadiusKm.HasValue && query.RadiusKm.Value > 0
            ? query.RadiusKm.Value
            : searchOptions.DefaultRadiusKm;

        var candidateLimit = query.CandidateLimit.HasValue && query.CandidateLimit.Value > 0
            ? query.CandidateLimit.Value
            : searchOptions.CandidateLimit;

        IReadOnlyList<PlaceCandidate> candidates;
        double referenceLat;
        double referenceLon;
        BoundingBox? searchBox = query.Box;

        if (searchBox is not null)
        {
            candidates = await _placeSearchService.SearchByBoundingBoxAsync(
                query.Query,
                searchBox,
                candidateLimit,
                query.Amenity,
                cancellationToken
            );
            referenceLat = searchBox.CenterLatitude;
            referenceLon = searchBox.CenterLongitude;
        }
        else
        {
            candidates = await _placeSearchService.SearchAsync(
                query.Query,
                query.Latitude!.Value,
                query.Longitude!.Value,
                radiusKm,
                candidateLimit,
                cancellationToken
            );
            referenceLat = query.Latitude.Value;
            referenceLon = query.Longitude.Value;
        }

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
                referenceLat,
                referenceLon,
                place.Latitude,
                place.Longitude
            );

            var finalScore = await _rankingService.CalculateFinalScoreAsync(
                relevanceScore,
                distanceKm,
                radiusKm,
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
                place.OpeningHours,
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
