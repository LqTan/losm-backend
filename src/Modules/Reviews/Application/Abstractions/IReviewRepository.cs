using Reviews.Domain.Entities;

namespace Reviews.Application.Abstractions;

public interface IReviewRepository
{
    Task AddAsync(Review review, CancellationToken cancellationToken = default);
    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Review>> GetByPlaceIdAsync(
        Guid placeId,
        CancellationToken cancellationToken = default);
    Task<double?> GetAverageRatingByPlaceIdAsync(
        Guid placeId,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAndPlaceAsync(
        Guid userId,
        Guid placeId,
        CancellationToken cancellationToken = default);
}
