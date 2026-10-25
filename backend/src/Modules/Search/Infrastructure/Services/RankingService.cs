using Configuration.Application.Abstractions;
using Configuration.Application.Models;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class RankingService : IRankingService
{
    private readonly ITuningProvider _tuning;
    private readonly IOnnxRankingModelService _onnxService;
    private readonly ICradisRanker _cradisRanker;

    public RankingService(
        ITuningProvider tuning,
        IOnnxRankingModelService onnxService,
        ICradisRanker cradisRanker)
    {
        _tuning = tuning;
        _onnxService = onnxService;
        _cradisRanker = cradisRanker;
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

    public async Task<IReadOnlyList<MeetingCandidateRankResult>> RankMeetingCandidatesAsync(
        IReadOnlyList<MeetingCandidateRankInput> candidates,
        double radiusKm,
        string profileName,
        CancellationToken ct)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var weights = await _tuning.GetRankingProfileAsync(profileName, ct);
        bool onnxActive = _onnxService.GetStatus().IsActive;

        var cradisScores = ComputeCradisScores(candidates, weights);

        // Branch 1: If ONNX model is active, attempt LambdaMART inference
        if (onnxActive && profileName == "Meeting")
        {
            var onnxResults = new List<MeetingCandidateRankResult>(candidates.Count);

            for (int i = 0; i < candidates.Count; i++)
            {
                var item = candidates[i];
                var cradis = cradisScores[i];

                float? onnxScore = null;
                if (item.CategoryMatch > 0f)
                {
                    onnxScore = _onnxService.Predict(item.OnnxFeatures);
                }

                if (onnxScore.HasValue)
                {
                    onnxResults.Add(new MeetingCandidateRankResult(
                        item.Candidate,
                        item.DistancesKm,
                        item.DurationsMinutes,
                        item.DistanceAvgKm,
                        item.DistanceMaxKm,
                        item.DurationAvgMin,
                        item.DurationMaxMin,
                        item.DurationSpreadMin,
                        item.RelevanceScore,
                        item.IsRoad,
                        DistanceScore: cradis.DistanceScore,
                        FairnessScore: cradis.FairnessScore,
                        FinalScore: onnxScore.Value,
                        IsOnnxScored: true
                    ));
                }
                else
                {
                    // Fallback to CRADIS-DN4 for out-of-domain candidates
                    onnxResults.Add(new MeetingCandidateRankResult(
                        item.Candidate,
                        item.DistancesKm,
                        item.DurationsMinutes,
                        item.DistanceAvgKm,
                        item.DistanceMaxKm,
                        item.DurationAvgMin,
                        item.DurationMaxMin,
                        item.DurationSpreadMin,
                        item.RelevanceScore,
                        item.IsRoad,
                        DistanceScore: cradis.DistanceScore,
                        FairnessScore: cradis.FairnessScore,
                        FinalScore: cradis.FinalScore,
                        IsOnnxScored: false
                    ));
                }
            }

            return onnxResults;
        }

        // Branch 2: When ONNX is inactive, rank the entire batch using CRADIS-DN4
        var results = new List<MeetingCandidateRankResult>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            var item = candidates[i];
            var cradis = cradisScores[i];

            results.Add(new MeetingCandidateRankResult(
                item.Candidate,
                item.DistancesKm,
                item.DurationsMinutes,
                item.DistanceAvgKm,
                item.DistanceMaxKm,
                item.DurationAvgMin,
                item.DurationMaxMin,
                item.DurationSpreadMin,
                item.RelevanceScore,
                item.IsRoad,
                DistanceScore: cradis.DistanceScore,
                FairnessScore: cradis.FairnessScore,
                FinalScore: cradis.FinalScore,
                IsOnnxScored: false
            ));
        }

        return results;
    }

    private IReadOnlyList<(double DistanceScore, double FairnessScore, double FinalScore)> ComputeCradisScores(
        IReadOnlyList<MeetingCandidateRankInput> candidates,
        RankingProfile weights)
    {
        var criteria = new List<CradisCriterion>
        {
            new("Relevance", IsBenefit: true, Weight: weights.Relevance),
            new("TravelDuration", IsBenefit: false, Weight: weights.Distance),
            new("TravelFairness", IsBenefit: false, Weight: weights.Fairness)
        };

        var alternatives = candidates.Select(c =>
            new CradisAlternativeInput<MeetingCandidateRankInput>(
                c,
                [
                    c.RelevanceScore,
                    c.DurationAvgMin,
                    c.DurationSpreadMin
                ]
            )
        ).ToList();

        var ranked = _cradisRanker.Rank(alternatives, criteria);

        return ranked.Select(r => (
            DistanceScore: r.NormalizedValues.Count > 1 ? r.NormalizedValues[1] : 0.0,
            FairnessScore: r.NormalizedValues.Count > 2 ? r.NormalizedValues[2] : 0.0,
            FinalScore: r.FinalScore
        )).ToList();
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

            // Distance & fairness normalized bounded scores
            distanceScore = Math.Max(0.0, 1.0 - (durAvg / 60.0));
            fairnessScore = Math.Max(0.0, 1.0 - (durSpread / 30.0));
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

        float? onnxScore = null;
        if (profileName == "Meeting" && onnxFeatures is not null && _onnxService.GetStatus().IsActive)
        {
            if (onnxFeatures.CategoryMatch > 0f)
            {
                onnxScore = _onnxService.Predict(onnxFeatures);
            }
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
            final,
            IsOnnxScored: onnxScore.HasValue));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
