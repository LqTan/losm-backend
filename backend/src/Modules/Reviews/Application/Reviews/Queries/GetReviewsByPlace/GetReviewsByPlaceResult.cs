namespace Reviews.Application.Reviews.Queries.GetReviewsByPlace;

public sealed record GetReviewsByPlaceResult(
    Guid Id,
    Guid PlaceId,
    Guid UserId,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);