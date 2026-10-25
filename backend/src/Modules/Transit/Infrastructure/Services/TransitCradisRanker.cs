namespace Transit.Infrastructure.Services;

using Transit.Application.Abstractions;
using Transit.Application.Network.Queries;

/// <summary>
/// Implements multi-criteria transit journey ranking using CRADIS-DN4 (2023).
/// Evaluates walking distance (C1), intermediate stops (C2), and headway frequency (C3).
/// </summary>
public sealed class TransitCradisRanker : ITransitRanker
{
    private const double WeightWalking = 0.45;
    private const double WeightStops = 0.35;
    private const double WeightHeadway = 0.20;

    public IReadOnlyList<TransitJourneyResult> RankJourneys(
        IReadOnlyList<TransitJourneyResult> candidates,
        int limit)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<TransitJourneyResult>();
        }

        if (candidates.Count == 1)
        {
            var single = candidates[0];
            return new[]
            {
                single with
                {
                    Score = 1.0,
                    RankingMethod = "cradis-dn4"
                }
            };
        }

        var m = candidates.Count;
        const int n = 3;
        var weights = new[] { WeightWalking, WeightStops, WeightHeadway };

        // Step 1: Build raw decision matrix Y (all cost criteria)
        var y = new double[m, n];
        for (var i = 0; i < m; i++)
        {
            var c = candidates[i];
            y[i, 0] = Math.Max(0.0, c.AccessDistanceMeters + c.EgressDistanceMeters);
            y[i, 1] = Math.Max(1.0, c.StopCount);
            y[i, 2] = Math.Max(1.0, c.Route.HeadwayMin ?? c.Route.HeadwayMax ?? 15.0);
        }

        // Step 2: DN4 Normalization for cost criteria: N_ij = 1 - y_ij / sqrt(sum y_kj^2)
        var nMatrix = new double[m, n];
        for (var j = 0; j < n; j++)
        {
            double sumSquares = 0.0;
            for (var i = 0; i < m; i++)
            {
                sumSquares += y[i, j] * y[i, j];
            }

            var sqrtSum = Math.Sqrt(sumSquares);
            for (var i = 0; i < m; i++)
            {
                if (sqrtSum > 1e-9)
                {
                    var val = 1.0 - (y[i, j] / sqrtSum);
                    nMatrix[i, j] = Math.Max(0.0, val);
                }
                else
                {
                    nMatrix[i, j] = 1.0;
                }
            }
        }

        // Step 3: Weighted normalized matrix v_ij = w_j * N_ij
        var v = new double[m, n];
        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < n; j++)
            {
                v[i, j] = weights[j] * nMatrix[i, j];
            }
        }

        // Step 4: Ideal solution (v_j+) and Anti-ideal solution (v_j-)
        var vPlus = new double[n];
        var vMinus = new double[n];
        for (var j = 0; j < n; j++)
        {
            var maxVal = double.MinValue;
            var minVal = double.MaxValue;
            for (var i = 0; i < m; i++)
            {
                if (v[i, j] > maxVal) maxVal = v[i, j];
                if (v[i, j] < minVal) minVal = v[i, j];
            }
            vPlus[j] = maxVal;
            vMinus[j] = minVal;
        }

        // Step 5: Distances from ideal (S+) and anti-ideal (S-)
        var sPlus = new double[m];
        var sMinus = new double[m];
        for (var i = 0; i < m; i++)
        {
            double sumP = 0.0;
            double sumM = 0.0;
            for (var j = 0; j < n; j++)
            {
                sumP += Math.Abs(v[i, j] - vPlus[j]);
                sumM += Math.Abs(v[i, j] - vMinus[j]);
            }
            sPlus[i] = sumP;
            sMinus[i] = sumM;
        }

        // Step 6: Utility measures K+ and K-
        // In CRADIS: S0+ = min(S+), S0- = max(S-)
        double s0Plus = sPlus.Min();
        double s0Minus = sMinus.Max();

        var kPlus = new double[m];
        var kMinus = new double[m];
        for (var i = 0; i < m; i++)
        {
            if (sPlus[i] <= 0.0 && s0Plus <= 0.0)
            {
                kPlus[i] = 1.0;
            }
            else if (sPlus[i] > 0.0)
            {
                kPlus[i] = s0Plus / sPlus[i];
            }
            else
            {
                kPlus[i] = 1.0;
            }

            kMinus[i] = s0Minus > 0.0
                ? sMinus[i] / s0Minus
                : 1.0;
        }

        // Step 7: Final compromise score Q_i = (K+ + K-) / 2
        var rawScores = new double[m];
        var maxQ = double.MinValue;
        for (var i = 0; i < m; i++)
        {
            var q = (kPlus[i] + kMinus[i]) / 2.0;
            rawScores[i] = q;
            if (q > maxQ) maxQ = q;
        }

        var scored = new List<(TransitJourneyResult Journey, double Score)>(m);
        for (var i = 0; i < m; i++)
        {
            var normalizedScore = maxQ > 1e-9 ? Math.Clamp(rawScores[i] / maxQ, 0.0, 1.0) : 1.0;
            scored.Add((candidates[i], normalizedScore));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Journey.AccessDistanceMeters + x.Journey.EgressDistanceMeters)
            .ThenBy(x => x.Journey.StopCount)
            .Take(limit)
            .Select(x => x.Journey with
            {
                Score = Math.Round(x.Score, 3),
                RankingMethod = "cradis-dn4"
            })
            .ToList();
    }
}
