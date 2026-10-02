namespace Reviews.Application.Reviews.Commands.CreateReview;

public sealed record CreateReviewCommand(
    Guid PlaceId,
    Guid UserId,
    int Rating,
    string? Comment
);