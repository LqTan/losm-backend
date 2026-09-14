using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.CreateReview;
using Configuration.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class CreateReviewTool
    : AgentTool<CreateReviewToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentExecutionContext _executionContext;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;

    public CreateReviewTool(
        IPendingActionStore pendingActionStore,
        IAgentExecutionContext executionContext,
        Configuration.Application.Abstractions.ITuningProvider tuning)
        : base(tuning, "create_review")
    {
        _pendingActionStore = pendingActionStore;
        _executionContext = executionContext;
        _tuning = tuning;
    }

    public override string Name => "create_review";

    public override string Description =>
        "Submit a community review (rating 1-5 and optional comment) for a " +
        "place the user just visited. Returns a confirmation draft that the " +
        "user must approve in the UI before it is persisted. Call this when " +
        "the user gives a rating or feedback about a place from the session.";

    public override ToolKind Kind => ToolKind.WriteRequiresConfirmation;

    protected override async Task<string> ExecuteAsync(
        CreateReviewToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        var userId = _executionContext.UserId;
        var sessionId = _executionContext.SessionId;

        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Current user is not available.");
        }
        if (!sessionId.HasValue || sessionId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Current session is not available.");
        }

        if (arguments.PlaceId == Guid.Empty)
        {
            throw new ArgumentException(
                "PlaceId is required.",
                nameof(arguments.PlaceId));
        }

        if (arguments.Rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arguments.Rating),
                "Rating must be between 1 and 5.");
        }

        var confirmationId = Guid.NewGuid();
        var actionId = Guid.NewGuid();

        var payload = new
        {
            placeId = arguments.PlaceId,
            rating = arguments.Rating,
            comment = arguments.Comment
        };

        var payloadJson = JsonSerializer.Serialize(payload);

        var action = new Domain.Entities.PendingAgentAction(
            id: actionId,
            sessionId: sessionId.Value,
            userId: userId.Value,
            actionType: "create_review",
            payloadJson: payloadJson,
            description:
                $"Gửi đánh giá {arguments.Rating} sao" +
                (string.IsNullOrWhiteSpace(arguments.Comment)
                    ? string.Empty
                    : $": \"{arguments.Comment}\""),
            confirmationId: confirmationId
        );

        await _pendingActionStore.AddAsync(action, cancellationToken);

        var response = new
        {
            status = "awaiting_confirmation",
            actionId = action.Id,
            confirmationId = confirmationId,
            action = "create_review",
            placeId = arguments.PlaceId,
            rating = arguments.Rating,
            comment = arguments.Comment,
            message = "User must confirm before the review is saved."
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }
}
