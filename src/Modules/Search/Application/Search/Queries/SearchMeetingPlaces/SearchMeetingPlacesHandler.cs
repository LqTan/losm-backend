using Configuration.Application.Abstractions;
using Search.Application.Abstractions;

namespace Search.Application.Search.Queries.SearchMeetingPlaces;

public sealed class SearchMeetingPlacesHandler
{
    private readonly IPlaceSearchService _placeSearchService;
    private readonly IRelevanceScorer _relevanceScorer;
    private readonly IRankingService _rankingService;
    private readonly ITuningProvider _tuning;

    public SearchMeetingPlacesHandler(
        IPlaceSearchService placeSearchService,
        IRelevanceScorer relevanceScorer,
        IRankingService rankingService,
        ITuningProvider tuning)
    {
        _placeSearchService = placeSearchService;
        _relevanceScorer = relevanceScorer;
        _rankingService = rankingService;
        _tuning = tuning;
    }

    public async Task<SearchMeetingPlacesResult> HandleAsync(
        SearchMeetingPlacesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Origins is null || query.Origins.Count == 0)
        {
            throw new ArgumentException(
                "At least one origin is required.",
                nameof(query));
        }

        const string profile = "Meeting";

        var centroidLat = query.Origins.Average(o => o.Latitude);
        var centroidLon = query.Origins.Average(o => o.Longitude);

        var maxOriginRadius = query.Origins.Max(o =>
            _rankingService.CalculateDistanceKm(
                centroidLat, centroidLon,
                o.Latitude, o.Longitude));

        var searchRadius = query.RadiusKm + maxOriginRadius;

        var candidates = await _placeSearchService.SearchAsync(
            query.Query,
            centroidLat,
            centroidLon,
            searchRadius,
            cancellationToken
        );

        var relevanceScores = await _relevanceScorer.ScoreAsync(
            query.Query,
            candidates,
            cancellationToken
        );

        var scoreByPlaceId = relevanceScores.ToDictionary(
            x => x.PlaceId, x => x.Score);

        var results = new List<SearchMeetingPlaceResult>(candidates.Count);

        foreach (var place in candidates)
        {
            var distancesByOrigin = query.Origins
                .Select(o => _rankingService.CalculateDistanceKm(
                    o.Latitude, o.Longitude,
                    place.Latitude, place.Longitude))
                .ToList();

            var relevanceScore = scoreByPlaceId.GetValueOrDefault(place.Id);

            var breakdown = await _rankingService.CalculateBreakdownAsync(
                relevanceScore,
                distancesByOrigin.Min(),
                query.RadiusKm,
                place.Rating,
                distancesByOrigin,
                profile,
                cancellationToken
            );

            results.Add(new SearchMeetingPlaceResult(
                place.Id,
                place.Name,
                place.Address,
                place.Latitude,
                place.Longitude,
                place.Category,
                distancesByOrigin,
                distancesByOrigin.Average(),
                distancesByOrigin.Max(),
                distancesByOrigin.Max() - distancesByOrigin.Min(),
                relevanceScore,
                breakdown.DistanceScore,
                breakdown.FairnessScore,
                breakdown.RatingScore,
                breakdown.FinalScore
            ));
        }

        var top = results
            .OrderByDescending(x => x.FinalScore)
            .Take(query.TopK)
            .ToList();

        return new SearchMeetingPlacesResult(
            top,
            centroidLat,
            centroidLon,
            searchRadius,
            profile
        );
    }
}
