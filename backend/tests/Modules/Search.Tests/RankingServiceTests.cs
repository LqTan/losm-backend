using Configuration.Application.Abstractions;
using Configuration.Application.Models;
using Moq;
using Search.Application.Abstractions;
using Search.Infrastructure.Services;

namespace Search.Tests;

public class RankingServiceTests
{
    private readonly Mock<ITuningProvider> _tuningMock = new();
    private readonly Mock<IOnnxRankingModelService> _onnxMock = new();
    private readonly ICradisRanker _cradisRanker = new CradisRanker();

    public RankingServiceTests()
    {
        _tuningMock.Setup(t => t.GetRankingProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RankingProfile
            {
                Name = "Meeting",
                Relevance = 1.0 / 3.0,
                Distance = 1.0 / 3.0,
                Fairness = 1.0 / 3.0
            });
    }

    [Fact]
    public async Task RankMeetingCandidatesAsync_WhenOnnxIsInactive_RanksAllWithCradis()
    {
        // Arrange
        _onnxMock.Setup(o => o.GetStatus())
            .Returns(new OnnxModelStatusDto(
                HasModel: true,
                IsActive: false,
                ModelName: "lambdamart.onnx",
                FileSizeBytes: 1024,
                UploadedAt: DateTime.UtcNow,
                InputName: "input",
                FeatureCount: 10,
                StatusMessage: "Model is inactive"
            ));

        var rankingService = new RankingService(_tuningMock.Object, _onnxMock.Object, _cradisRanker);

        var candidateA = new PlaceCandidate(Guid.NewGuid(), "Central Cafe", "123 Street", "cafe", null, 10.77, 106.69);
        var candidateB = new PlaceCandidate(Guid.NewGuid(), "Far Coffee", "456 Street", "cafe", null, 10.82, 106.75);

        var inputs = new List<MeetingCandidateRankInput>
        {
            new(
                Candidate: candidateA,
                DistancesKm: [2.0, 3.0],
                DurationsMinutes: [8.0, 10.0],
                DistanceAvgKm: 2.5,
                DistanceMaxKm: 3.0,
                DurationAvgMin: 9.0,
                DurationMaxMin: 10.0,
                DurationSpreadMin: 2.0,
                RelevanceScore: 0.95,
                CategoryMatch: 1.0f,
                IsRoad: true,
                OnnxFeatures: CreateDummyFeatures(0.95f, 9.0f, 2.0f)
            ),
            new(
                Candidate: candidateB,
                DistancesKm: [12.0, 18.0],
                DurationsMinutes: [35.0, 50.0],
                DistanceAvgKm: 15.0,
                DistanceMaxKm: 18.0,
                DurationAvgMin: 42.5,
                DurationMaxMin: 50.0,
                DurationSpreadMin: 15.0,
                RelevanceScore: 0.60,
                CategoryMatch: 1.0f,
                IsRoad: true,
                OnnxFeatures: CreateDummyFeatures(0.60f, 42.5f, 15.0f)
            )
        };

        // Act
        var results = await rankingService.RankMeetingCandidatesAsync(inputs, 5.0, "Meeting", CancellationToken.None);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(r.IsOnnxScored));

        // Candidate A (closer, fairer, higher relevance) must have higher CRADIS final score than B
        var resultA = results.First(r => r.Candidate.Id == candidateA.Id);
        var resultB = results.First(r => r.Candidate.Id == candidateB.Id);

        Assert.True(resultA.FinalScore > resultB.FinalScore);
        Assert.True(resultA.DistanceScore > resultB.DistanceScore);
        Assert.True(resultA.FairnessScore > resultB.FairnessScore);
    }

    [Fact]
    public async Task RankMeetingCandidatesAsync_WhenOnnxIsActive_RanksWithLambdaMartAndSetsOnnxFlag()
    {
        // Arrange
        _onnxMock.Setup(o => o.GetStatus())
            .Returns(new OnnxModelStatusDto(
                HasModel: true,
                IsActive: true,
                ModelName: "lambdamart.onnx",
                FileSizeBytes: 1024,
                UploadedAt: DateTime.UtcNow,
                InputName: "input",
                FeatureCount: 10,
                StatusMessage: "Model is active"
            ));

        _onnxMock.Setup(o => o.Predict(It.IsAny<RankingFeaturesDto>()))
            .Returns((RankingFeaturesDto f) => f.Bm25Norm * 2.0f); // Mock prediction

        var rankingService = new RankingService(_tuningMock.Object, _onnxMock.Object, _cradisRanker);

        var candidate = new PlaceCandidate(Guid.NewGuid(), "Central Cafe", "123 Street", "cafe", null, 10.77, 106.69);
        var inputs = new List<MeetingCandidateRankInput>
        {
            new(
                Candidate: candidate,
                DistancesKm: [2.0, 3.0],
                DurationsMinutes: [8.0, 10.0],
                DistanceAvgKm: 2.5,
                DistanceMaxKm: 3.0,
                DurationAvgMin: 9.0,
                DurationMaxMin: 10.0,
                DurationSpreadMin: 2.0,
                RelevanceScore: 0.95,
                CategoryMatch: 1.0f,
                IsRoad: true,
                OnnxFeatures: CreateDummyFeatures(0.95f, 9.0f, 2.0f)
            )
        };

        // Act
        var results = await rankingService.RankMeetingCandidatesAsync(inputs, 5.0, "Meeting", CancellationToken.None);

        // Assert
        Assert.Single(results);
        Assert.True(results[0].IsOnnxScored);
        Assert.Equal(0.95f * 2.0f, results[0].FinalScore, precision: 4);
    }

    private static RankingFeaturesDto CreateDummyFeatures(float bm25, float durAvg, float durSpread)
    {
        return new RankingFeaturesDto(
            Bm25Norm: bm25,
            CategoryMatch: 1.0f,
            DurationAvgMin: durAvg,
            DurationMaxMin: durAvg + 2.0f,
            DurationSpreadMin: durSpread,
            DistanceAvgKm: 3.0f,
            DistanceMaxKm: 4.0f,
            DurationAvgRankNorm: 0.0f,
            DurationSpreadRankNorm: 0.0f,
            ParticipantCount: 2.0f
        );
    }
}
