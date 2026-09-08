namespace AgentCore.Application.Tools.PlaceReviews;

public sealed record PlaceReviewsToolArguments(
    Guid PlaceId,
    int Limit = 10
);
