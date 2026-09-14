using Places.Domain.Entities;

namespace Places.Application.Contracts;

public interface IPlacesSearchContract
{
    Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        CancellationToken cancellationToken
    );
}
