using Configuration.Application.Abstractions;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class RankingService : IRankingService
{
    private readonly ITuningProvider _tuning;

    public RankingService(ITuningProvider tuning)
    {
        _tuning = tuning;
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

    public async Task<double> CalculateFinalScoreAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        double? rating,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct)
    {
        var weights = await _tuning.GetRankingProfileAsync(profileName, ct);

        var distanceScore = Math.Max(0, 1 - distanceKm / radiusKm);
        var ratingScore = rating.HasValue ? rating.Value / 5.0 : 0;

        double fairnessScore = 0;
        if (originDistancesKm is { Count: > 0 } && weights.Fairness > 0)
        {
            var maxD = originDistancesKm.Max();
            var minD = originDistancesKm.Min();
            var spread = maxD - minD;
            fairnessScore = Math.Max(0, 1 - spread / radiusKm);
        }

        return weights.Relevance * relevanceScore
             + weights.Distance * distanceScore
             + weights.Fairness * fairnessScore
             + weights.Rating * ratingScore;
    }

    public async Task<RankingScoreBreakdown> CalculateBreakdownAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        double? rating,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct)
    {
        var weights = await _tuning.GetRankingProfileAsync(profileName, ct);

        var distanceScore = Math.Max(0, 1 - distanceKm / radiusKm);
        var ratingScore = rating.HasValue ? rating.Value / 5.0 : 0;

        double fairnessScore = 0;
        if (originDistancesKm is { Count: > 0 })
        {
            var maxD = originDistancesKm.Max();
            var minD = originDistancesKm.Min();
            var spread = maxD - minD;
            fairnessScore = Math.Max(0, 1 - spread / radiusKm);
        }

        var final =
            weights.Relevance * relevanceScore
            + weights.Distance * distanceScore
            + weights.Fairness * fairnessScore
            + weights.Rating * ratingScore;

        return new RankingScoreBreakdown(
            distanceScore,
            ratingScore,
            fairnessScore,
            final);
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
