namespace Search.Application.Abstractions;

public sealed record RankingScoreBreakdown(
    double DistanceScore,
    double RatingScore,
    double FairnessScore,
    double FinalScore
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
        double? rating,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct
    );

    Task<RankingScoreBreakdown> CalculateBreakdownAsync(
        double relevanceScore,
        double distanceKm,
        double radiusKm,
        double? rating,
        IReadOnlyList<double>? originDistancesKm,
        string profileName,
        CancellationToken ct
    );
}
