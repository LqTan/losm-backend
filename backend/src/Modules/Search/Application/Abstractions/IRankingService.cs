namespace Search.Application.Abstractions;

public sealed record RankingScoreBreakdown(
    double DistanceScore,
    double FairnessScore,
    double FinalScore,
    bool IsOnnxScored = false
);

public interface IRankingService
{
    double CalculateDistanceKm(
        double userLatitude,
        double userLongitude,
        double placeLatitude,
        double placeLongitude
    );

    Task<double> CalculateFinalScoreAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct,
        RankingFeaturesDto? onnxFeatures = null
    );

    Task<RankingScoreBreakdown> CalculateBreakdownAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct,
        RankingFeaturesDto? onnxFeatures = null
    );
}
