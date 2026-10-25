namespace Search.Application.Abstractions;

public sealed record CradisCriterion(
    string Name,
    bool IsBenefit,
    double Weight
);

public sealed record CradisAlternativeInput<T>(
    T Item,
    IReadOnlyList<double> CriteriaValues
);

public sealed record CradisAlternativeScore<T>(
    T Item,
    IReadOnlyList<double> NormalizedValues,
    double IdealDistance,
    double AntiIdealDistance,
    double UtilityPlus,
    double UtilityMinus,
    double FinalScore
);

public interface ICradisRanker
{
    IReadOnlyList<CradisAlternativeScore<T>> Rank<T>(
        IReadOnlyList<CradisAlternativeInput<T>> alternatives,
        IReadOnlyList<CradisCriterion> criteria
    );
}
