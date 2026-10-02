using Common.Application.Exceptions;
using Reviews.Application.Abstractions;
using Reviews.Domain.Entities;

namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed class CreateReviewHandler
{
    private readonly IReviewRepository _reviewRepository;

    public CreateReviewHandler(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository;
    }
    public async Task<CreateReviewResult> HandleAsync(
        CreateReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        if (await _reviewRepository.ExistsForUserAndPlaceAsync(
            command.UserId,
            command.PlaceId,
            cancellationToken))
        {
            throw new ConflictException(
                "You have already reviewed this place. Each user can only review a place once.");
        }

        var review = new Review(
            command.PlaceId,
            command.UserId,
            command.Rating,
            command.Comment
        );
        await _reviewRepository.AddAsync(review, cancellationToken);
        return new CreateReviewResult(
            review.Id,
            review.PlaceId,
            review.UserId,
            review.Rating,
            review.Comment,
            review.CreatedAt
        );
    }
}
