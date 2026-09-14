namespace Search.Application.Abstractions;

public interface IPlaceSearchService
{
    Task<IReadOnlyList<PlaceCandidate>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        CancellationToken cancellationToken
    );
}
