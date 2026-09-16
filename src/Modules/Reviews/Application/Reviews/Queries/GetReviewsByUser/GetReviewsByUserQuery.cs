namespace Reviews.Application.Reviews.Queries.GetReviewsByUser;

public sealed record GetReviewsByUserQuery(
    Guid UserId,
    int? Limit = null
);