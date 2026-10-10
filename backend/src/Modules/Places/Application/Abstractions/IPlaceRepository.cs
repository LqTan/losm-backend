using Places.Domain.Entities;

namespace Places.Application.Abstractions;

public sealed record PlaceSearchHit(
    Guid Id,
    string Name,
    string? Address,
    string? Category,
    string? OpeningHours,
    double Latitude,
    double Longitude,
    double DistanceMeters);

public sealed record UpsertResult(
    IReadOnlyList<Place> Canonical,
    int InsertedCount,
    int UpdatedCount);

public interface IPlaceSpatialSearchService
{
    Task<IReadOnlyList<PlaceSearchHit>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface IPlaceRepository
{
    Task<Place?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Place>> SearchByNameAsync(
        string name,
        int limit = 5,
        CancellationToken cancellationToken = default
    );
    Task<UpsertResult> UpsertRangeAsync(
        IReadOnlyList<Place> places,
        CancellationToken cancellationToken = default
    );
    Task<(IReadOnlyList<Place> Items, int TotalCount)> GetPagedAsync(
        string? search,
        string? category,
        string? source,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<string>> GetDistinctSourcesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetDistinctCategoriesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Place place, CancellationToken cancellationToken = default);
    Task UpdateAsync(Place place, CancellationToken cancellationToken = default);
    Task DeleteAsync(Place place, CancellationToken cancellationToken = default);
}