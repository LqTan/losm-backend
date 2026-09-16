using Places.Domain.Entities;

namespace Places.Application.Abstractions;

public interface IPlaceProvider
{
    Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Place>> SearchByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int candidateLimit,
        string? amenity,
        CancellationToken cancellationToken = default
    );
}
