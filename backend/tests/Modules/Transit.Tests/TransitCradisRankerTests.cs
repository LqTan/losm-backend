namespace Transit.Tests;

using Transit.Application.Network.Queries;
using Transit.Infrastructure.Services;
using Xunit;

public sealed class TransitCradisRankerTests
{
    private static TransitRouteResult CreateRoute(int number, int headwayMin) =>
        new(Guid.NewGuid(), number, $"Route {number}", "Bus", Guid.NewGuid(), 0, "Outbound",
            300, 1320, headwayMin, headwayMin + 5, "Daily", 5000, "Fixed", false, Array.Empty<TransitStopResult>());

    private static TransitStopResult CreateStop(string name) =>
        new(Guid.NewGuid(), name, "Street address", 10.77, 106.69, "Bus", null, Array.Empty<int>());

    [Fact]
    public void RankJourneys_WithEmptyCandidates_ReturnsEmpty()
    {
        var ranker = new TransitCradisRanker();
        var result = ranker.RankJourneys(Array.Empty<TransitJourneyResult>(), 10);
        Assert.Empty(result);
    }

    [Fact]
    public void RankJourneys_WithSingleCandidate_ReturnsScoreOneAndCradisMethod()
    {
        var ranker = new TransitCradisRanker();
        var journey = new TransitJourneyResult(
            CreateRoute(1, 10),
            CreateStop("A"),
            CreateStop("B"),
            5,
            100,
            150);

        var result = ranker.RankJourneys(new[] { journey }, 10);

        Assert.Single(result);
        Assert.Equal(1.0, result[0].Score);
        Assert.Equal("cradis-dn4", result[0].RankingMethod);
    }

    [Fact]
    public void RankJourneys_OrdersShortestWalkingFewerStopsAndFrequentHeadwayFirst()
    {
        var ranker = new TransitCradisRanker();

        // Journey 1: Low walking (50m + 50m = 100m), few stops (3), low headway (5m) -> Best
        var j1 = new TransitJourneyResult(CreateRoute(1, 5), CreateStop("A1"), CreateStop("B1"), 3, 50, 50);

        // Journey 2: High walking (400m + 400m = 800m), many stops (15), high headway (25m) -> Worst
        var j2 = new TransitJourneyResult(CreateRoute(2, 25), CreateStop("A2"), CreateStop("B2"), 15, 400, 400);

        // Journey 3: Medium walking (150m + 150m = 300m), medium stops (7), medium headway (12m) -> Middle
        var j3 = new TransitJourneyResult(CreateRoute(3, 12), CreateStop("A3"), CreateStop("B3"), 7, 150, 150);

        var ranked = ranker.RankJourneys(new[] { j2, j1, j3 }, 10);

        Assert.Equal(3, ranked.Count);
        Assert.Equal(1, ranked[0].Route.Number); // J1 should be rank 1
        Assert.Equal(3, ranked[1].Route.Number); // J3 should be rank 2
        Assert.Equal(2, ranked[2].Route.Number); // J2 should be rank 3

        Assert.True(ranked[0].Score >= ranked[1].Score);
        Assert.True(ranked[1].Score >= ranked[2].Score);
        Assert.All(ranked, r => Assert.Equal("cradis-dn4", r.RankingMethod));
    }
}
