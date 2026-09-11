using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.SavePlace;

namespace AgentCore.Infrastructure.Tools;

public sealed class SavePlaceTool
    : AgentTool<SavePlaceToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentExecutionContext _executionContext;

    public SavePlaceTool(
        IPendingActionStore pendingActionStore,
        IAgentExecutionContext executionContext
    )
    {
        _pendingActionStore = pendingActionStore;
        _executionContext = executionContext;
    }

    public override string Name => "save_place";

    public override string Description =>
        "Save a place to the user's Saved Places list. " +
        "The action requires explicit user confirmation before it is applied.";

    protected override Task<string> ExecuteAsync(
        SavePlaceToolArguments arguments,
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

        var action = new PendingAgentAction(
            Id: Guid.NewGuid(),
            SessionId: _executionContext.SessionId ?? Guid.Empty,
            UserId: userId.Value,
            Type: PendingAgentActionType.SavePlace,
            Description:
                $"Save this place to Saved Places" +
                (string.IsNullOrWhiteSpace(arguments.Note)
                    ? string.Empty
                    : $" with note: {arguments.Note}"),
            PlaceId: arguments.PlaceId,
            Note: arguments.Note,
            Rating: null,
            Comment: null,
            CreatedAt: DateTime.UtcNow
        );

        var stored = _pendingActionStore.Add(action);

        var payload = new
        {
            status = "awaiting_confirmation",
            actionId = stored.Id,
            action = "save_place",
            placeId = stored.PlaceId,
            note = stored.Note,
            message = "User must confirm before the place is saved."
        };

        return Task.FromResult(
            JsonSerializer.Serialize(payload, JsonOptions)
        );
    }
}
