using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using Reviews.Application.Reviews.Commands.CreateReview;

namespace AgentCore.Infrastructure.Dispatchers;

public sealed class CreateReviewDispatcher : IAgentActionDispatcher
{
    public string ActionType => "create_review";

    private readonly CreateReviewHandler _createReviewHandler;

    public CreateReviewDispatcher(CreateReviewHandler createReviewHandler)
    {
        _createReviewHandler = createReviewHandler;
    }

    public async Task<DispatchResult> DispatchAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(action.PayloadJson);
            var root = doc.RootElement;

            var placeId = root.GetProperty("placeId").GetGuid();
            var rating = root.GetProperty("rating").GetInt32();
            var comment = root.TryGetProperty("comment", out var c) && c.ValueKind != JsonValueKind.Null
                ? c.GetString()
                : null;

            var result = await _createReviewHandler.HandleAsync(
                new CreateReviewCommand(placeId, action.UserId, rating, comment));

            var payload = JsonSerializer.Serialize(new
            {
                result = "review_created",
                reviewId = result.Id,
                placeId = result.PlaceId,
                rating = result.Rating
            });

            return DispatchResult.Ok(payload);
        }
        catch (Exception ex)
        {
            return DispatchResult.Fail(ex.Message);
        }
    }
}
