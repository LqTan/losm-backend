using Places.Application.Abstractions;
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

    Task<IReadOnlyList<Place>> SearchByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int candidateLimit,
        string? amenity,
        CancellationToken cancellationToken
    );
}
