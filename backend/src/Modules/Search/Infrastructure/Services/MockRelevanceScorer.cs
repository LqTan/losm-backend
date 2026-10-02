using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class MockRelevanceScorer : IRelevanceScorer
{
    public Task<IReadOnlyList<RelevanceScore>> ScoreAsync(
        string query,
        IReadOnlyList<PlaceCandidate> candidates,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<RelevanceScore> scores = candidates
            .Select(candidates => new RelevanceScore(
                candidates.Id,
                0.5
            )).ToList();

        return Task.FromResult(scores);
    }
}