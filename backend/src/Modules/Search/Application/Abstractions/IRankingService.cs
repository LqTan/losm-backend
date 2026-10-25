namespace Search.Application.Abstractions;

public sealed record RankingScoreBreakdown(
    double DistanceScore,
    double FairnessScore,
    double FinalScore,
    bool IsOnnxScored = false
);

public sealed record MeetingCandidateRankInput(
    PlaceCandidate Candidate,
    IReadOnlyList<double> DistancesKm,
    IReadOnlyList<double>? DurationsMinutes,
    double DistanceAvgKm,
    double DistanceMaxKm,
    double DurationAvgMin,
    double DurationMaxMin,
    double DurationSpreadMin,
    double RelevanceScore,
    float CategoryMatch,
    bool IsRoad,
    RankingFeaturesDto OnnxFeatures
);

public sealed record MeetingCandidateRankResult(
    PlaceCandidate Candidate,
    IReadOnlyList<double> DistancesKm,
    IReadOnlyList<double>? DurationsMinutes,
    double DistanceAvgKm,
    double DistanceMaxKm,
    double DurationAvgMin,
    double DurationMaxMin,
    double DurationSpreadMin,
    double RelevanceScore,
    bool IsRoad,
    double DistanceScore,
    double FairnessScore,
    double FinalScore,
    bool IsOnnxScored
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

    Task<IReadOnlyList<MeetingCandidateRankResult>> RankMeetingCandidatesAsync(
        IReadOnlyList<MeetingCandidateRankInput> candidates,
        double radiusKm,
        string profileName,
        CancellationToken ct
    );
}
