using Microsoft.EntityFrameworkCore;
using Reviews.Application.Abstractions;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Repositories;

public sealed class ReviewRepository : IReviewRepository
{
    private readonly ReviewsDbContext _dbContext;

    public ReviewRepository(ReviewsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Review review,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Reviews.AddAsync(review, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Review?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .FirstOrDefaultAsync(review => review.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Review>> GetByPlaceIdAsync(
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .Where(review => review.PlaceId == placeId)
            .OrderByDescending(review => review.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Review>> GetByUserIdAsync(
        Guid userId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reviews
            .Where(review => review.UserId == userId)
            .OrderByDescending(review => review.CreatedAt);

        if (limit is > 0)
        {
            query = (IOrderedQueryable<Review>)query.Take(limit.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<double?> GetAverageRatingByPlaceIdAsync(
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .Where(review => review.PlaceId == placeId)
            .Select(review => (double?)review.Rating)
            .AverageAsync(cancellationToken);
    }

    public async Task<bool> ExistsForUserAndPlaceAsync(
        Guid userId,
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Reviews
            .AnyAsync(
                review => review.UserId == userId && review.PlaceId == placeId,
                cancellationToken);
    }
}
