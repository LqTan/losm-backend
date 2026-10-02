namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed record CreateReviewResult(
    Guid Id,
    Guid PlaceId,
    Guid UserId,
    int Rating,
    string? Comment,
    DateTime CreatedAt
);