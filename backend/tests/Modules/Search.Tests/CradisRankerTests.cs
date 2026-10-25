using Search.Application.Abstractions;
using Search.Infrastructure.Services;

namespace Search.Tests;

public class CradisRankerTests
{
    private readonly CradisRanker _ranker = new();

    [Fact]
    public void Rank_WithExample1FromPaper_MatchesPublishedTable3RanksExactly()
    {
        // Example 1 from Le Dang Ha (2023) paper "Selection of Suitable Data Normalization Method to Combine with CRADIS Method":
        // 17 metal drilling alternatives (S1..S17) and 6 criteria (all Cost/SBC form):
        // Weights: [0.3, 0.1, 0.1, 0.1, 0.1, 0.3]
        var criteria = new List<CradisCriterion>
        {
            new("C1", IsBenefit: false, Weight: 0.3),
            new("C2", IsBenefit: false, Weight: 0.1),
            new("C3", IsBenefit: false, Weight: 0.1),
            new("C4", IsBenefit: false, Weight: 0.1),
            new("C5", IsBenefit: false, Weight: 0.1),
            new("C6", IsBenefit: false, Weight: 0.3)
        };

        var rawData = new (string Name, double[] Values)[]
        {
            ("S1",  [14.03, 0.051, 0.058, 0.105, 0.21,  0.479]),
            ("S2",  [ 7.59, 0.053, 0.058, 0.155, 0.245, 1.211]),
            ("S3",  [ 7.34, 0.035, 0.06,  0.165, 0.215, 0.916]),
            ("S4",  [ 4.06, 0.033, 0.075, 0.18,  0.215, 0.535]),
            ("S5",  [ 5.4,  0.048, 0.078, 0.25,  0.195, 0.601]),
            ("S6",  [ 5.5,  0.05,  0.084, 0.185, 0.185, 0.703]),
            ("S7",  [ 2.81, 0.033, 0.058, 0.185, 0.185, 0.466]),
            ("S8",  [ 2.62, 0.028, 0.048, 0.2,   0.19,  0.577]),
            ("S9",  [ 2.88, 0.028, 0.05,  0.18,  0.15,  0.417]),
            ("S10", [ 2.75, 0.043, 0.051, 0.23,  0.195, 0.675]),
            ("S11", [ 2.84, 0.043, 0.055, 0.165, 0.205, 0.418]),
            ("S12", [ 1.59, 0.028, 0.074, 0.145, 0.17,  0.601]),
            ("S13", [ 1.88, 0.038, 0.064, 0.185, 0.175, 0.563]),
            ("S14", [ 3.44, 0.049, 0.066, 0.19,  0.185, 0.391]),
            ("S15", [ 2.04, 0.023, 0.059, 0.16,  0.18,  0.493]),
            ("S16", [ 2.1,  0.043, 0.05,  0.235, 0.185, 0.675]),
            ("S17", [ 1.25, 0.04,  0.049, 0.44,  0.19,  0.65])
        };

        var alternatives = rawData.Select(d => new CradisAlternativeInput<string>(d.Name, d.Values)).ToList();

        var results = _ranker.Rank(alternatives, criteria);

        // Expected ranks according to Table 3 (CRADIS - DN4 column) in the paper:
        var expectedRanks = new Dictionary<string, int>
        {
            ["S1"] = 16,
            ["S2"] = 17,
            ["S3"] = 15,
            ["S4"] = 12,
            ["S5"] = 13,
            ["S6"] = 14,
            ["S7"] = 4,
            ["S8"] = 7,
            ["S9"] = 2,
            ["S10"] = 11,
            ["S11"] = 5,
            ["S12"] = 3,
            ["S13"] = 6,
            ["S14"] = 8,
            ["S15"] = 1,
            ["S16"] = 9,
            ["S17"] = 10
        };

        var sorted = results.OrderByDescending(r => r.FinalScore).ToList();
        var actualRanks = new Dictionary<string, int>();
        for (int rank = 1; rank <= sorted.Count; rank++)
        {
            actualRanks[sorted[rank - 1].Item] = rank;
        }

        foreach (var (name, expectedRank) in expectedRanks)
        {
            Assert.Equal(expectedRank, actualRanks[name]);
        }
    }

    [Fact]
    public void Rank_WithSingleAlternative_ReturnsFullScore()
    {
        var criteria = new List<CradisCriterion>
        {
            new("Relevance", IsBenefit: true, Weight: 0.5),
            new("Distance", IsBenefit: false, Weight: 0.5)
        };

        var alternatives = new List<CradisAlternativeInput<string>>
        {
            new("OnlyOne", [0.8, 5.0])
        };

        var results = _ranker.Rank(alternatives, criteria);

        Assert.Single(results);
        Assert.Equal(1.0, results[0].FinalScore);
        Assert.Equal("OnlyOne", results[0].Item);
    }

    [Fact]
    public void Rank_WithZeroDenominatorInCostCriterion_DoesNotThrowAndHandlesCleanly()
    {
        // When all alternatives have 0 for a cost criterion (e.g. 0 duration spread)
        var criteria = new List<CradisCriterion>
        {
            new("Relevance", IsBenefit: true, Weight: 0.5),
            new("DurationSpread", IsBenefit: false, Weight: 0.5)
        };

        var alternatives = new List<CradisAlternativeInput<string>>
        {
            new("PlaceA", [0.9, 0.0]),
            new("PlaceB", [0.3, 0.0])
        };

        var results = _ranker.Rank(alternatives, criteria);

        Assert.Equal(2, results.Count);
        Assert.False(double.IsNaN(results[0].FinalScore));
        Assert.False(double.IsNaN(results[1].FinalScore));

        // PlaceA has higher relevance, so it should rank higher
        var best = results.OrderByDescending(r => r.FinalScore).First();
        Assert.Equal("PlaceA", best.Item);
    }

    [Fact]
    public void Rank_MeetingPlaceCandidates_OrdersByOverallCompromiseUtility()
    {
        var criteria = new List<CradisCriterion>
        {
            new("Relevance", IsBenefit: true, Weight: 1.0 / 3.0),
            new("TravelDuration", IsBenefit: false, Weight: 1.0 / 3.0),
            new("TravelFairness", IsBenefit: false, Weight: 1.0 / 3.0)
        };

        var alternatives = new List<CradisAlternativeInput<string>>
        {
            new("IdealCandidate", [0.95, 10.0, 1.0]),     // High relevance, low duration, low spread
            new("AverageCandidate", [0.60, 25.0, 10.0]),   // Moderate
            new("WorstCandidate", [0.10, 60.0, 40.0])      // Low relevance, long travel, unfair
        };

        var results = _ranker.Rank(alternatives, criteria);

        var ordered = results.OrderByDescending(r => r.FinalScore).Select(r => r.Item).ToList();

        Assert.Equal("IdealCandidate", ordered[0]);
        Assert.Equal("AverageCandidate", ordered[1]);
        Assert.Equal("WorstCandidate", ordered[2]);
    }
}
