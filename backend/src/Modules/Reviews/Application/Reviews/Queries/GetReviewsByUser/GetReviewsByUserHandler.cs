using Reviews.Application.Abstractions;

namespace Reviews.Application.Reviews.Queries.GetReviewsByUser;

public sealed class GetReviewsByUserHandler
{
    private readonly IReviewRepository _reviewRepository;

    public GetReviewsByUserHandler(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository;
    }

    public async Task<IReadOnlyList<GetReviewsByUserResult>> HandleAsync(
        GetReviewsByUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var reviews = await _reviewRepository.GetByUserIdAsync(
            query.UserId,
            query.Limit,
            cancellationToken);

        return reviews
            .Select(review => new GetReviewsByUserResult(
                review.Id,
                review.PlaceId,
                review.UserId,
                review.Rating,
                review.Comment,
                review.CreatedAt,
                review.UpdatedAt))
            .ToList();
    }
}