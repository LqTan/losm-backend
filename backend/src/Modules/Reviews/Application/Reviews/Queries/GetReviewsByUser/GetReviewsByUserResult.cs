namespace Reviews.Application.Reviews.Queries.GetReviewsByUser;

public sealed record GetReviewsByUserResult(
    Guid Id,
    Guid PlaceId,
    Guid UserId,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);