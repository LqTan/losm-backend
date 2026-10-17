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

        double distanceScore;
        double fairnessScore = 0;

        if (profileName == "Meeting")
        {
            double durAvg = onnxFeatures?.DurationAvgMin ?? (originDistancesKm is { Count: > 0 } ? originDistancesKm.Average() / 25.0 * 60.0 : distanceKm / 25.0 * 60.0);
            double durSpread = onnxFeatures?.DurationSpreadMin ?? (originDistancesKm is { Count: > 0 } ? (originDistancesKm.Max() - originDistancesKm.Min()) / 25.0 * 60.0 : 0.0);

            // Colab baseline formula: convenience (15m scale) & fairness (10m scale)
            distanceScore = 1.0 / (1.0 + (durAvg / 15.0));
            fairnessScore = 1.0 / (1.0 + (durSpread / 10.0));
        }
        else
        {
            distanceScore = Math.Max(0, 1 - distanceKm / radiusKm);
            if (originDistancesKm is { Count: > 0 } && weights.Fairness > 0)
            {
                var maxD = originDistancesKm.Max();
                var minD = originDistancesKm.Min();
                var spread = maxD - minD;
                fairnessScore = Math.Max(0, 1 - spread / radiusKm);
            }
        }

        // Dynamic Routing for Assistant / Meeting Places:
        // Chỉ dùng mô hình LambdaMART cho hồ sơ "Meeting" khi địa điểm nằm trong phạm vi học được (CategoryMatch > 0)
        float? onnxScore = null;
        if (profileName == "Meeting" && onnxFeatures is not null && _onnxService.GetStatus().IsActive)
        {
            // Nếu CategoryMatch > 0: Chuyển qua LambdaMART (Thuật toán của nhóm làm)
            // Nếu CategoryMatch == 0: Ngoài phạm vi học -> Fallback về Thuật toán hệ thống
            if (onnxFeatures.CategoryMatch > 0f)
            {
                onnxScore = _onnxService.Predict(onnxFeatures);
            }
        }

        // Điểm xếp hạng cuối cùng:
        // - Nếu có onnxScore: dùng kết quả của LambdaMART
        // - Nếu không (ngoài phạm vi hoặc search thường): dùng Thuật toán của hệ thống (weights.Relevance + weights.Distance + weights.Fairness)
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
            final,
            IsOnnxScored: onnxScore.HasValue));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
