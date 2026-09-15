namespace Search.Application.Abstractions;

public interface IRelevanceScorer
{
    Task<IReadOnlyList<RelevanceScore>> ScoreAsync(
        string query,
        IReadOnlyList<PlaceCandidate> candidates,
        CancellationToken cancellationToken
    );
}

public sealed record PlaceCandidate(
    Guid Id,
    string Name,
    string? Address,
    string? Category,
    string? OpeningHours,
    double Latitude,
    double Longitude
);

public sealed record RelevanceScore(
    Guid PlaceId,
    double Score
);
