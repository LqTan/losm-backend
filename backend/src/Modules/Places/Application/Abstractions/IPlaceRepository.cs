using Places.Domain.Entities;

namespace Places.Application.Abstractions;

public interface IPlaceRepository
{
    Task<Place?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Place>> SearchByNameAsync(
        string name,
        int limit = 5,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<Place>> UpsertRangeAsync(
        IReadOnlyList<Place> places,
        CancellationToken cancellationToken = default
    );
}
