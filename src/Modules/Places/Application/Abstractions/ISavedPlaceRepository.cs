using Places.Domain.Entities;

namespace Places.Application.Abstractions;

public interface ISavedPlaceRepository
{
    Task<SavedPlace?> GetAsync(
        Guid userId,
        Guid placeId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<SavedPlace>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        SavedPlace savedPlace,
        CancellationToken cancellationToken = default
    );

    Task RemoveAsync(
        SavedPlace savedPlace,
        CancellationToken cancellationToken = default
    );
}
