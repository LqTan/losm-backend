using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.CreateReview;

namespace AgentCore.Infrastructure.Tools;

public sealed class CreateReviewTool
    : AgentTool<CreateReviewToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentExecutionContext _executionContext;

    public CreateReviewTool(
        IPendingActionStore pendingActionStore,
        IAgentExecutionContext executionContext
    )
    {
        _pendingActionStore = pendingActionStore;
        _executionContext = executionContext;
    }

    public override string Name => "create_review";

    public override string Description =>
        "Draft a community review (rating + optional comment) for a place. " +
        "The action requires explicit user confirmation before it is applied.";

    protected override Task<string> ExecuteAsync(
        CreateReviewToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        var userId = _executionContext.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Current user is not available."
            );
        }

        if (arguments.PlaceId == Guid.Empty)
        {
            throw new ArgumentException(
                "PlaceId is required.",
                nameof(arguments.PlaceId)
            );
        }

        if (arguments.Rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arguments.Rating),
                "Rating must be between 1 and 5."
            );
        }

        var action = new PendingAgentAction(
            Id: Guid.NewGuid(),
            SessionId: _executionContext.SessionId ?? Guid.Empty,
            UserId: userId.Value,
            Type: PendingAgentActionType.CreateReview,
            Description:
                $"Submit a {arguments.Rating}-star review" +
                (string.IsNullOrWhiteSpace(arguments.Comment)
                    ? string.Empty
                    : $": \"{arguments.Comment}\""),
            PlaceId: arguments.PlaceId,
            Note: null,
            Rating: arguments.Rating,
            Comment: arguments.Comment,
            CreatedAt: DateTime.UtcNow
        );

        var stored = _pendingActionStore.Add(action);

        var payload = new
        {
            status = "awaiting_confirmation",
            actionId = stored.Id,
            action = "create_review",
            placeId = stored.PlaceId,
            rating = stored.Rating,
            comment = stored.Comment,
            message = "User must confirm before the review is saved."
        };

        return Task.FromResult(
            JsonSerializer.Serialize(payload, JsonOptions)
        );
    }
}
