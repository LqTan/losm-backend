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

        // 1. Precompute distance and duration metrics for all candidates
        var candidateMetrics = new List<(
            PlaceCandidate Candidate,
            List<double> Distances,
            List<double>? Durations,
            double DistanceAvg,
            double DistanceMax,
            double DurationAvg,
            double DurationMax,
            double DurationSpread,
            double Relevance,
            float CategoryMatch,
            bool IsRoad
        )>(candidates.Count);

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

            var durAvg = durationsByOrigin is { Count: > 0 }
                ? durationsByOrigin.Average()
                : distancesByOrigin.Average() / 25.0 * 60.0;

            var durMax = durationsByOrigin is { Count: > 0 }
                ? durationsByOrigin.Max()
                : distancesByOrigin.Max() / 25.0 * 60.0;

            var durSpread = durationsByOrigin is { Count: > 0 }
                ? durationsByOrigin.Max() - durationsByOrigin.Min()
                : (distancesByOrigin.Max() - distancesByOrigin.Min()) / 25.0 * 60.0;

            var relevanceScore = scoreByPlaceId.GetValueOrDefault(place.Id);
            var catMatch = ComputeCategoryMatch(place.Category, query.Query);

            candidateMetrics.Add((
                place,
                distancesByOrigin,
                durationsByOrigin,
                distancesByOrigin.Average(),
                distancesByOrigin.Max(),
                durAvg,
                durMax,
                durSpread,
                relevanceScore,
                catMatch,
                isRoad
            ));
        }

        // 2. Compute relative ranks within candidate batch (0-1 normalized, 1.0 is best)
        int K = candidateMetrics.Count;
        var avgDurationRanks = new float[K];
        var spreadDurationRanks = new float[K];

        if (K <= 1)
        {
            if (K == 1)
            {
                avgDurationRanks[0] = 1.0f;
                spreadDurationRanks[0] = 1.0f;
            }
        }
        else
        {
            var sortedByAvg = candidateMetrics
                .Select((m, idx) => (m.DurationAvg, idx))
                .OrderBy(x => x.DurationAvg)
                .ToList();

            for (int r = 0; r < K; r++)
            {
                avgDurationRanks[sortedByAvg[r].idx] = 1.0f - ((float)r / (K - 1));
            }

            var sortedBySpread = candidateMetrics
                .Select((m, idx) => (m.DurationSpread, idx))
                .OrderBy(x => x.DurationSpread)
                .ToList();

            for (int r = 0; r < K; r++)
            {
                spreadDurationRanks[sortedBySpread[r].idx] = 1.0f - ((float)r / (K - 1));
            }
        }

        var results = new List<SearchMeetingPlaceResult>(K);

        for (var j = 0; j < K; j++)
        {
            var m = candidateMetrics[j];
            var place = m.Candidate;

            var onnxInput = new RankingFeaturesDto(
                Bm25Norm: (float)m.Relevance,
                CategoryMatch: m.CategoryMatch,
                DurationAvgMin: (float)m.DurationAvg,
                DurationMaxMin: (float)m.DurationMax,
                DurationSpreadMin: (float)m.DurationSpread,
                DistanceAvgKm: (float)m.DistanceAvg,
                DistanceMaxKm: (float)m.DistanceMax,
                DurationAvgRankNorm: avgDurationRanks[j],
                DurationSpreadRankNorm: spreadDurationRanks[j],
                ParticipantCount: (float)query.Origins.Count
            );

            var breakdown = await _rankingService.CalculateBreakdownAsync(
                m.Relevance,
                m.Distances.Min(),
                radiusKm,
                m.Distances,
                profile,
                cancellationToken,
                onnxInput
            );

            double? avgDuration = m.Durations is { Count: > 0 }
                ? Math.Round(m.Durations.Average(), 1)
                : null;

            double? spreadDuration = m.Durations is { Count: > 0 }
                ? Math.Round(m.Durations.Max() - m.Durations.Min(), 1)
                : null;

            results.Add(new SearchMeetingPlaceResult(
                place.Id,
                place.Name,
                place.Address,
                place.Latitude,
                place.Longitude,
                place.Category,
                place.OpeningHours,
                m.Distances,
                Math.Round(m.DistanceAvg, 2),
                Math.Round(m.DistanceMax, 2),
                Math.Round(m.DistanceMax - m.Distances.Min(), 2),
                m.Relevance,
                breakdown.DistanceScore,
                breakdown.FairnessScore,
                breakdown.FinalScore,
                m.Durations,
                avgDuration,
                spreadDuration,
                m.IsRoad
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

    private static float ComputeCategoryMatch(string? placeCategory, string query)
    {
        if (string.IsNullOrWhiteSpace(placeCategory) || string.IsNullOrWhiteSpace(query))
            return 0f;

        var p = placeCategory.ToLowerInvariant();
        var q = query.ToLowerInvariant();

        if (q.Contains(p) || p.Contains(q)) return 1f;

        if ((q.Contains("cà phê") || q.Contains("cafe") || q.Contains("coffee")) &&
            (p.Contains("cafe") || p.Contains("coffee") || p.Contains("cà phê")))
            return 1f;

        if ((q.Contains("ăn") || q.Contains("nhà hàng") || q.Contains("quán ăn") || q.Contains("food") || q.Contains("restaurant")) &&
            (p.Contains("restaurant") || p.Contains("food") || p.Contains("ăn") || p.Contains("quán")))
            return 1f;

        return 0f;
    }
}
