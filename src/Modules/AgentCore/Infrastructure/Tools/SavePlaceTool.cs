using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.SavePlace;
using Configuration.Application.Abstractions;
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
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;

    public SavePlaceTool(
        IPlaceRepository placeRepository,
        IPendingActionStore pendingActionStore,
        IAgentExecutionContext executionContext,
        Configuration.Application.Abstractions.ITuningProvider tuning)
        : base(tuning, "save_place")
    {
        _placeRepository = placeRepository;
        _pendingActionStore = pendingActionStore;
        _executionContext = executionContext;
        _tuning = tuning;
    }

    public override string Name => "save_place";

    public override string Description =>
        "Save a place to the currently authenticated user's Saved Places list. " +
        "Accepts the place's display name (as it appears in search results); " +
        "the tool resolves the name to the correct database id. " +
        "Returns a confirmation draft that the user must approve in the UI " +
        "before it is persisted. Call this whenever the user asks to save, " +
        "bookmark, keep, or remember a place. If the name is ambiguous, " +
        "ask the user to clarify before calling.";

    public override ToolKind Kind => ToolKind.WriteRequiresConfirmation;

    protected override async Task<string> ExecuteAsync(
        SavePlaceToolArguments arguments,
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

        if (string.IsNullOrWhiteSpace(arguments.PlaceName))
        {
            throw new ArgumentException(
                "PlaceName is required.",
                nameof(arguments.PlaceName));
        }

        var runtime = await _tuning.GetRuntimeAsync(cancellationToken);
        var limit = runtime.ToolNameMatchLimit > 0 ? runtime.ToolNameMatchLimit : 5;

        var candidates = await _placeRepository.SearchByNameAsync(
            arguments.PlaceName,
            limit: limit,
            cancellationToken
        );

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No place matching '{arguments.PlaceName}' was found " +
                "in the database. Ask the user to clarify the name or " +
                "call search_places again.");
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
                ".");
        }

        var confirmationId = Guid.NewGuid();
        var actionId = Guid.NewGuid();

        var payload = new
        {
            placeId = place.Id,
            placeName = place.Name,
            note = arguments.Note
        };

        var payloadJson = JsonSerializer.Serialize(payload);

        var action = new Domain.Entities.PendingAgentAction(
            id: actionId,
            sessionId: sessionId.Value,
            userId: userId.Value,
            actionType: "save_place",
            payloadJson: payloadJson,
            description:
                $"Lưu '{place.Name}' vào Saved Places" +
                (string.IsNullOrWhiteSpace(arguments.Note)
                    ? string.Empty
                    : $" với ghi chú: {arguments.Note}"),
            confirmationId: confirmationId
        );

        await _pendingActionStore.AddAsync(action, cancellationToken);

        var response = new
        {
            status = "awaiting_confirmation",
            actionId = action.Id,
            confirmationId = confirmationId,
            action = "save_place",
            placeId = place.Id,
            placeName = place.Name,
            note = action.IdempotencyKey is null ? arguments.Note : arguments.Note,
            message = "User must confirm before the place is saved."
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }
}
