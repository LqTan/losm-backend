using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.SavePlace;
using Places.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class SavePlaceTool
    : AgentTool<SavePlaceToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPlaceRepository _placeRepository;
    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentExecutionContext _executionContext;

    public SavePlaceTool(
        IPlaceRepository placeRepository,
        IPendingActionStore pendingActionStore,
        IAgentExecutionContext executionContext
    )
    {
        _placeRepository = placeRepository;
        _pendingActionStore = pendingActionStore;
        _executionContext = executionContext;
    }

    public override string Name => "save_place";

    public override string Description =>
        "Save a place to the currently authenticated user's Saved Places " +
        "list. Accepts the place's display name (as it appears in search " +
        "results); the tool resolves the name to the correct database id. " +
        "Returns a confirmation draft that the user must approve in the UI " +
        "before it is persisted. Call this whenever the user asks to save, " +
        "bookmark, keep, or remember a place. If the name is ambiguous, " +
        "ask the user to clarify before calling.";

    protected override async Task<string> ExecuteAsync(
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

        if (string.IsNullOrWhiteSpace(arguments.PlaceName))
        {
            throw new ArgumentException(
                "PlaceName is required.",
                nameof(arguments.PlaceName)
            );
        }

        var candidates = await _placeRepository.SearchByNameAsync(
            arguments.PlaceName,
            limit: 5,
            cancellationToken
        );

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No place matching '{arguments.PlaceName}' was found " +
                "in the database. Ask the user to clarify the name or " +
                "call search_places again."
            );
        }

        var exact = candidates.FirstOrDefault(c =>
            string.Equals(
                c.Name,
                arguments.PlaceName,
                StringComparison.OrdinalIgnoreCase
            )
        );

        var place = exact ?? candidates[0];

        if (exact is null && candidates.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple places match '{arguments.PlaceName}'. " +
                "Ask the user to pick one of: " +
                string.Join(", ", candidates.Select(c => c.Name)) +
                "."
            );
        }

        var action = new PendingAgentAction(
            Id: Guid.NewGuid(),
            SessionId: _executionContext.SessionId ?? Guid.Empty,
            UserId: userId.Value,
            Type: PendingAgentActionType.SavePlace,
            Description:
                $"Save '{place.Name}' to Saved Places" +
                (string.IsNullOrWhiteSpace(arguments.Note)
                    ? string.Empty
                    : $" with note: {arguments.Note}"),
            PlaceId: place.Id,
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
            placeId = place.Id,
            placeName = place.Name,
            note = stored.Note,
            message = "User must confirm before the place is saved."
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
