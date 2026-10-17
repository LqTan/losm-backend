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
    private readonly ICategoryMappingProvider _categoryMappingProvider;

    public SearchMeetingPlacesHandler(
        IPlaceSearchService placeSearchService,
        IRelevanceScorer relevanceScorer,
        IRankingService rankingService,
        ITuningProvider tuning,
        IOsrmTableService osrmTableService,
        ICategoryMappingProvider categoryMappingProvider)
    {
        _placeSearchService = placeSearchService;
        _relevanceScorer = relevanceScorer;
        _rankingService = rankingService;
        _tuning = tuning;
        _osrmTableService = osrmTableService;
        _categoryMappingProvider = categoryMappingProvider;
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
            var catMatch = _categoryMappingProvider.ComputeCategoryMatch(
                place.Category,
                query.TargetCategory,
                query.SpecificPreference,
                query.Query);

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

        // 2. Compute relative ranks within candidate batch (0-1 normalized, Colab method=min, 0.0 is best/fastest)
        int K = candidateMetrics.Count;
        var avgDurationRanks = new float[K];
        var spreadDurationRanks = new float[K];

        if (K > 1)
        {
            for (int i = 0; i < K; i++)
            {
                int strictlySmallerAvg = candidateMetrics.Count(c => c.DurationAvg < candidateMetrics[i].DurationAvg);
                avgDurationRanks[i] = (float)strictlySmallerAvg / (K - 1);

                int strictlySmallerSpread = candidateMetrics.Count(c => c.DurationSpread < candidateMetrics[i].DurationSpread);
                spreadDurationRanks[i] = (float)strictlySmallerSpread / (K - 1);
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
                m.IsRoad,
                breakdown.IsOnnxScored
            ));
        }

        // Colab tie-breaking sort keys:
        // 1. score desc
        // 2. duration_avg_min asc
        // 3. duration_spread_min asc
        // 4. bm25_norm desc
        // 5. poi_id asc
        var top = results
            .OrderByDescending(x => Math.Round(x.FinalScore, 6))
            .ThenBy(x => x.AverageDurationMinutes ?? double.MaxValue)
            .ThenBy(x => x.SpreadMinutes ?? double.MaxValue)
            .ThenByDescending(x => x.RelevanceScore)
            .ThenBy(x => x.PlaceId)
            .Take(topK)
            .ToList();

        var rankingMethod = top.Any(x => x.IsOnnxScored) ? "onnx" : "system";

        return new SearchMeetingPlacesResult(
            top,
            centroidLat,
            centroidLon,
            searchRadius,
            profile,
            rankingMethod
        );
    }
}
