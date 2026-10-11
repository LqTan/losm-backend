using Configuration.Application.Abstractions;
using Search.Application.Abstractions;

namespace Search.Application.Search.Queries.SearchMeetingPlaces;

public sealed class SearchMeetingPlacesHandler
{
    private readonly IPlaceSearchService _placeSearchService;
    private readonly IRelevanceScorer _relevanceScorer;
    private readonly IRankingService _rankingService;
    private readonly ITuningProvider _tuning;
    private readonly IOsrmTableService _osrmTableService;

    public SearchMeetingPlacesHandler(
        IPlaceSearchService placeSearchService,
        IRelevanceScorer relevanceScorer,
        IRankingService rankingService,
        ITuningProvider tuning,
        IOsrmTableService osrmTableService)
    {
        _placeSearchService = placeSearchService;
        _relevanceScorer = relevanceScorer;
        _rankingService = rankingService;
        _tuning = tuning;
        _osrmTableService = osrmTableService;
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

        var searchOptions = await _tuning.GetSearchOptionsAsync(cancellationToken);

        var radiusKm = query.RadiusKm.HasValue && query.RadiusKm.Value > 0
            ? query.RadiusKm.Value
            : searchOptions.DefaultRadiusKm;

        var topK = query.TopK.HasValue && query.TopK.Value > 0
            ? query.TopK.Value
            : searchOptions.TopK;

        var candidateLimit = searchOptions.CandidateLimit;

        var centroidLat = query.Origins.Average(o => o.Latitude);
        var centroidLon = query.Origins.Average(o => o.Longitude);

        var maxOriginRadius = query.Origins.Max(o =>
            _rankingService.CalculateDistanceKm(
                centroidLat, centroidLon,
                o.Latitude, o.Longitude));

        var searchRadius = radiusKm + maxOriginRadius;

        var candidates = await _placeSearchService.SearchAsync(
            query.Query,
            centroidLat,
            centroidLon,
            searchRadius,
            candidateLimit,
            cancellationToken
        );

        var relevanceScores = await _relevanceScorer.ScoreAsync(
            query.Query,
            candidates,
            cancellationToken
        );

        var scoreByPlaceId = relevanceScores.ToDictionary(
            x => x.PlaceId, x => x.Score);

        // Fetch OSRM Table matrix (sources = origins, destinations = candidates)
        OsrmTableMatrix? osrmMatrix = null;
        if (candidates.Count > 0)
        {
            var sources = query.Origins
                .Select(o => new GeoCoordinate(o.Latitude, o.Longitude))
                .ToList();
            var destinations = candidates
                .Select(c => new GeoCoordinate(c.Latitude, c.Longitude))
                .ToList();

            osrmMatrix = await _osrmTableService.GetTableAsync(
                sources,
                destinations,
                cancellationToken
            );
        }

        var results = new List<SearchMeetingPlaceResult>(candidates.Count);

        for (var j = 0; j < candidates.Count; j++)
        {
            var place = candidates[j];
            List<double> distancesByOrigin;
            List<double>? durationsByOrigin = null;
            bool isRoad = false;

            if (osrmMatrix is not null
                && osrmMatrix.DistancesKm.Count == query.Origins.Count
                && osrmMatrix.DistancesKm[0].Count > j)
            {
                distancesByOrigin = Enumerable.Range(0, query.Origins.Count)
                    .Select(i => osrmMatrix.DistancesKm[i][j])
                    .ToList();

                durationsByOrigin = Enumerable.Range(0, query.Origins.Count)
                    .Select(i => osrmMatrix.DurationsMinutes[i][j])
                    .ToList();

                isRoad = true;
            }
            else
            {
                distancesByOrigin = query.Origins
                    .Select(o => _rankingService.CalculateDistanceKm(
                        o.Latitude, o.Longitude,
                        place.Latitude, place.Longitude))
                    .ToList();
            }

            var relevanceScore = scoreByPlaceId.GetValueOrDefault(place.Id);

            var breakdown = await _rankingService.CalculateBreakdownAsync(
                relevanceScore,
                distancesByOrigin.Min(),
                radiusKm,
                distancesByOrigin,
                profile,
                cancellationToken
            );

            double? avgDuration = durationsByOrigin is { Count: > 0 }
                ? Math.Round(durationsByOrigin.Average(), 1)
                : null;

            double? spreadDuration = durationsByOrigin is { Count: > 0 }
                ? Math.Round(durationsByOrigin.Max() - durationsByOrigin.Min(), 1)
                : null;

            results.Add(new SearchMeetingPlaceResult(
                place.Id,
                place.Name,
                place.Address,
                place.Latitude,
                place.Longitude,
                place.Category,
                place.OpeningHours,
                distancesByOrigin,
                Math.Round(distancesByOrigin.Average(), 2),
                Math.Round(distancesByOrigin.Max(), 2),
                Math.Round(distancesByOrigin.Max() - distancesByOrigin.Min(), 2),
                relevanceScore,
                breakdown.DistanceScore,
                breakdown.FairnessScore,
                breakdown.FinalScore,
                durationsByOrigin,
                avgDuration,
                spreadDuration,
                isRoad
            ));
        }

        var top = results
            .OrderByDescending(x => x.FinalScore)
            .Take(topK)
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
