using Configuration.Application.Abstractions;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class RankingService : IRankingService
{
    private readonly ITuningProvider _tuning;
    private readonly IOnnxRankingModelService _onnxService;

    public RankingService(ITuningProvider tuning, IOnnxRankingModelService onnxService)
    {
        _tuning = tuning;
        _onnxService = onnxService;
    }

    public double CalculateDistanceKm(
        double userLatitude,
        double userLongitude,
        double placeLatitude,
        double placeLongitude
    )
    {
        const double earthRadiusKm = 6371;
        double dLat = ToRadians(placeLatitude - userLatitude);
        double dLon = ToRadians(placeLongitude - userLongitude);

        double lat1 = ToRadians(userLatitude);
        double lat2 = ToRadians(placeLatitude);

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a)
        );
        return earthRadiusKm * c;
    }

    public Task<double> CalculateFinalScoreAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct,
        RankingFeaturesDto? onnxFeatures = null)
    {
        return CalculateAsync(
            relevanceScore,
            distanceKm,
            radiusKm,
            originDistancesKm,
            profileName,
            returnBreakdown: false,
            ct,
            onnxFeatures).ContinueWith(t => t.Result.final, ct);
    }

    public Task<RankingScoreBreakdown> CalculateBreakdownAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct,
        RankingFeaturesDto? onnxFeatures = null)
    {
        return CalculateAsync(
            relevanceScore,
            distanceKm,
            radiusKm,
            originDistancesKm,
            profileName,
            returnBreakdown: true,
            ct,
            onnxFeatures).ContinueWith(t => t.Result.breakdown!, ct);
    }

    private async Task<(double final, RankingScoreBreakdown? breakdown)> CalculateAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        bool returnBreakdown,
        CancellationToken ct,
        RankingFeaturesDto? onnxFeatures = null)
    {
        var weights = await _tuning.GetRankingProfileAsync(profileName, ct);

        var distanceScore = Math.Max(0, 1 - distanceKm / radiusKm);

        double fairnessScore = 0;
        if (originDistancesKm is { Count: > 0 } && weights.Fairness > 0)
        {
            var maxD = originDistancesKm.Max();
            var minD = originDistancesKm.Min();
            var spread = maxD - minD;
            fairnessScore = Math.Max(0, 1 - spread / radiusKm);
        }

        // Try ONNX LambdaMART prediction if model is active
        float? onnxScore = null;
        if (_onnxService.GetStatus().IsActive)
        {
            var maxD = originDistancesKm is { Count: > 0 } ? originDistancesKm.Max() : distanceKm;
            var avgD = originDistancesKm is { Count: > 0 } ? originDistancesKm.Average() : distanceKm;
            var durAvg = avgD / 25.0 * 60.0;
            var durMax = maxD / 25.0 * 60.0;
            var durSpread = originDistancesKm is { Count: > 0 }
                ? (originDistancesKm.Max() - originDistancesKm.Min()) / 25.0 * 60.0
                : 0.0;

            var inputDto = onnxFeatures ?? new RankingFeaturesDto(
                Bm25Norm: (float)relevanceScore,
                CategoryMatch: 1f,
                DurationAvgMin: (float)durAvg,
                DurationMaxMin: (float)durMax,
                DurationSpreadMin: (float)durSpread,
                DistanceAvgKm: (float)avgD,
                DistanceMaxKm: (float)maxD,
                DurationAvgRankNorm: (float)distanceScore,
                DurationSpreadRankNorm: (float)fairnessScore,
                ParticipantCount: (float)(originDistancesKm?.Count ?? 1)
            );

            onnxScore = _onnxService.Predict(inputDto);
        }

        var final = onnxScore.HasValue
            ? onnxScore.Value
            : weights.Relevance * relevanceScore
              + weights.Distance * distanceScore
              + weights.Fairness * fairnessScore;

        if (!returnBreakdown)
        {
            return (final, null);
        }

        return (final, new RankingScoreBreakdown(
            distanceScore,
            fairnessScore,
            final));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
