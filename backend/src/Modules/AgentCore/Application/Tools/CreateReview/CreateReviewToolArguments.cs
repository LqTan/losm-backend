namespace AgentCore.Application.Tools.CreateReview;

public sealed record CreateReviewToolArguments(
    Guid PlaceId,
    int Rating,
    string? Comment = null
);
