namespace Transit.Application.Abstractions;

using Transit.Application.Network.Queries;

/// <summary>
/// Service abstraction for multi-criteria ranking of public transit journey options.
/// </summary>
public interface ITransitRanker
{
    /// <summary>
    /// Ranks transit journeys using multi-criteria decision making (CRADIS-DN4)
    /// based on walking distance, stop count, and service frequency.
    /// </summary>
    IReadOnlyList<TransitJourneyResult> RankJourneys(
        IReadOnlyList<TransitJourneyResult> candidates,
        int limit);
}
