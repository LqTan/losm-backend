using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class CradisRanker : ICradisRanker
{
    public IReadOnlyList<CradisAlternativeScore<T>> Rank<T>(
        IReadOnlyList<CradisAlternativeInput<T>> alternatives,
        IReadOnlyList<CradisCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(alternatives);
        ArgumentNullException.ThrowIfNull(criteria);

        if (alternatives.Count == 0)
        {
            return [];
        }

        if (criteria.Count == 0)
        {
            throw new ArgumentException("At least one criterion must be specified.", nameof(criteria));
        }

        int m = alternatives.Count;
        int n = criteria.Count;

        for (int i = 0; i < m; i++)
        {
            if (alternatives[i].CriteriaValues.Count != n)
            {
                throw new ArgumentException(
                    $"Alternative at index {i} has {alternatives[i].CriteriaValues.Count} criteria values, expected {n}.",
                    nameof(alternatives));
            }
        }

        if (m == 1)
        {
            var single = alternatives[0];
            var uniformNorm = Enumerable.Repeat(1.0, n).ToList();
            return
            [
                new CradisAlternativeScore<T>(
                    single.Item,
                    uniformNorm,
                    IdealDistance: 0.0,
                    AntiIdealDistance: 0.0,
                    UtilityPlus: 1.0,
                    UtilityMinus: 1.0,
                    FinalScore: 1.0
                )
            ];
        }

        // Normalize criteria weights
        var normalizedWeights = new double[n];
        double weightSum = criteria.Sum(c => Math.Max(0.0, c.Weight));
        for (int j = 0; j < n; j++)
        {
            normalizedWeights[j] = weightSum > 0.0
                ? Math.Max(0.0, criteria[j].Weight) / weightSum
                : 1.0 / n;
        }

        // Step 2: Data normalization with DN4 (Vector normalization)
        // BBC (Benefit): N_ij = y_ij / sqrt(sum(y_kj^2))
        // SBC (Cost):    N_ij = 1 - (y_ij / sqrt(sum(y_kj^2)))
        var normalizedMatrix = new double[m, n];
        for (int j = 0; j < n; j++)
        {
            double sumSquares = 0.0;
            for (int i = 0; i < m; i++)
            {
                double val = alternatives[i].CriteriaValues[j];
                sumSquares += val * val;
            }

            double denominator = Math.Sqrt(sumSquares);
            bool isBenefit = criteria[j].IsBenefit;

            for (int i = 0; i < m; i++)
            {
                double val = alternatives[i].CriteriaValues[j];
                if (denominator <= 0.0)
                {
                    normalizedMatrix[i, j] = isBenefit ? 0.0 : 1.0;
                }
                else
                {
                    normalizedMatrix[i, j] = isBenefit
                        ? val / denominator
                        : 1.0 - (val / denominator);
                }
            }
        }

        // Step 3: Weighted normalization matrix: v_ij = N_ij * w_j
        var weightedMatrix = new double[m, n];
        for (int i = 0; i < m; i++)
        {
            for (int j = 0; j < n; j++)
            {
                weightedMatrix[i, j] = normalizedMatrix[i, j] * normalizedWeights[j];
            }
        }

        // Step 4: Determine ideal solution (t_j = max v_ij) and anti-ideal solution (t_aj = min v_ij)
        // Note: With DN4, higher normalized value is always preferable for both BBC and SBC.
        var idealBest = new double[n];
        var antiIdealWorst = new double[n];

        for (int j = 0; j < n; j++)
        {
            double maxVal = double.MinValue;
            double minVal = double.MaxValue;

            for (int i = 0; i < m; i++)
            {
                double v = weightedMatrix[i, j];
                if (v > maxVal) maxVal = v;
                if (v < minVal) minVal = v;
            }

            idealBest[j] = maxVal;
            antiIdealWorst[j] = minVal;
        }

        // Step 5 & 6: Differences and distance sums
        // d+ = t_j - v_ij, S+ = sum(d+)
        // d- = v_ij - t_aj, S- = sum(d-)
        var sPlus = new double[m];
        var sMinus = new double[m];

        for (int i = 0; i < m; i++)
        {
            double sumDPlus = 0.0;
            double sumDMinus = 0.0;

            for (int j = 0; j < n; j++)
            {
                sumDPlus += idealBest[j] - weightedMatrix[i, j];
                sumDMinus += weightedMatrix[i, j] - antiIdealWorst[j];
            }

            sPlus[i] = Math.Max(0.0, sumDPlus);
            sMinus[i] = Math.Max(0.0, sumDMinus);
        }

        // Step 7: Calculate S0+ = min(S+) and S0- = max(S-)
        double s0Plus = sPlus.Min();
        double s0Minus = sMinus.Max();

        // Step 7 & 8: Calculate relative utility K+ and K-, and final compromise score Q_i
        var results = new List<CradisAlternativeScore<T>>(m);

        for (int i = 0; i < m; i++)
        {
            double kPlus;
            if (sPlus[i] <= 0.0 && s0Plus <= 0.0)
            {
                kPlus = 1.0;
            }
            else if (sPlus[i] > 0.0)
            {
                kPlus = s0Plus / sPlus[i];
            }
            else
            {
                kPlus = 1.0;
            }

            double kMinus = s0Minus > 0.0
                ? sMinus[i] / s0Minus
                : 1.0;

            double qi = (kPlus + kMinus) / 2.0;

            var normValues = new double[n];
            for (int j = 0; j < n; j++)
            {
                normValues[j] = normalizedMatrix[i, j];
            }

            results.Add(new CradisAlternativeScore<T>(
                alternatives[i].Item,
                normValues,
                IdealDistance: sPlus[i],
                AntiIdealDistance: sMinus[i],
                UtilityPlus: kPlus,
                UtilityMinus: kMinus,
                FinalScore: qi
            ));
        }

        return results;
    }
}
