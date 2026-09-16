using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.CreateMeeting;
using Configuration.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class CreateMeetingTool
    : AgentTool<CreateMeetingToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IPlaceRepository _placeRepository;
    private readonly IPendingActionStore _pendingActionStore;
    private readonly ILastSearchContextStore _lastSearchContextStore;
    private readonly IAgentExecutionContext _executionContext;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;
    private readonly ILogger<CreateMeetingTool> _logger;

    public CreateMeetingTool(
        IPlaceRepository placeRepository,
        IPendingActionStore pendingActionStore,
        ILastSearchContextStore lastSearchContextStore,
        IAgentExecutionContext executionContext,
        Configuration.Application.Abstractions.ITuningProvider tuning,
        ILogger<CreateMeetingTool> logger)
        : base(tuning, "create_meeting")
    {
        _placeRepository = placeRepository;
        _pendingActionStore = pendingActionStore;
        _lastSearchContextStore = lastSearchContextStore;
        _executionContext = executionContext;
        _tuning = tuning;
        _logger = logger;
    }

    public override string Name => "create_meeting";

    public override string Description =>
        "Create a meeting at a saved place, invite attendees, and create a Google Calendar event. " +
        "Returns a confirmation draft that the user must approve in the UI before it is executed. " +
        "Call this whenever the user asks to schedule, set up, or arrange a meeting. " +
        "Pass either PlaceId or SelectedIndex referencing the most recent session search; " +
        "if neither is supplied, the tool falls back to a name-based lookup.";

    public override ToolKind Kind => ToolKind.WriteRequiresConfirmation;

    protected override async Task<string> ExecuteAsync(
        CreateMeetingToolArguments arguments,
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

        if (string.IsNullOrWhiteSpace(arguments.Title))
        {
            throw new ArgumentException("Title is required.", nameof(arguments.Title));
        }
        if (arguments.AttendeeEmails is null || arguments.AttendeeEmails.Length == 0)
        {
            throw new ArgumentException("At least one attendee email is required.",
                nameof(arguments.AttendeeEmails));
        }

        var invalidEmails = arguments.AttendeeEmails
            .Where(e => !IsValidEmail(e))
            .ToList();
        if (invalidEmails.Count > 0)
        {
            throw new ArgumentException(
                $"Invalid email format: {string.Join(", ", invalidEmails)}. " +
                "Ask the user to provide valid email addresses before scheduling.",
                nameof(arguments.AttendeeEmails));
        }

        var place = await ResolvePlaceAsync(
            arguments,
            sessionId.Value,
            cancellationToken);

        if (place is null)
        {
            throw new InvalidOperationException(
                "Could not resolve the meeting place. Specify PlaceId or SelectedIndex referencing the most recent search, or pass a PlaceName.");
        }

        var confirmationId = Guid.NewGuid();
        var actionId = Guid.NewGuid();

        var payload = new
        {
            placeId = place.Id,
            placeName = place.Name,
            title = arguments.Title,
            purpose = arguments.Purpose,
            startAt = arguments.StartAt,
            durationMinutes = arguments.DurationMinutes,
            attendees = arguments.AttendeeEmails,
            note = arguments.Note
        };

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var idempotencyKey = ComputeIdempotencyKey(userId.Value, payloadJson);

        var existing = await _pendingActionStore
            .GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);

        if (existing is not null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "duplicate_request",
                actionId = existing.Id,
                confirmationId = existing.ConfirmationId,
                action = "create_meeting",
                placeId = place.Id,
                placeName = place.Name,
                title = arguments.Title,
                startAt = arguments.StartAt,
                durationMinutes = arguments.DurationMinutes,
                attendees = arguments.AttendeeEmails,
                note = arguments.Note,
                message = "A pending action for the same meeting already exists; confirm the existing one."
            }, JsonOptions);
        }

        var action = new Domain.Entities.PendingAgentAction(
            id: actionId,
            sessionId: sessionId.Value,
            userId: userId.Value,
            actionType: "create_meeting",
            payloadJson: payloadJson,
            description:
                $"Tạo cuộc hẹn '{arguments.Title}' tại '{place.Name}' lúc " +
                $"{arguments.StartAt:HH:mm dd/MM} với {arguments.AttendeeEmails.Length} người tham dự",
            confirmationId: confirmationId,
            idempotencyKey: idempotencyKey
        );

        await _pendingActionStore.AddAsync(action, cancellationToken);

        var response = new
        {
            status = "awaiting_confirmation",
            actionId = action.Id,
            confirmationId = confirmationId,
            action = "create_meeting",
            placeId = place.Id,
            placeName = place.Name,
            title = arguments.Title,
            startAt = arguments.StartAt,
            durationMinutes = arguments.DurationMinutes,
            attendees = arguments.AttendeeEmails,
            note = arguments.Note,
            message = "User must confirm before the meeting is created."
        };

        return JsonSerializer.Serialize(response, JsonOptions);
    }

    private async Task<Places.Domain.Entities.Place?> ResolvePlaceAsync(
        CreateMeetingToolArguments arguments,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (arguments.PlaceId.HasValue && arguments.PlaceId.Value != Guid.Empty)
        {
            var byId = await _placeRepository.GetByIdAsync(
                arguments.PlaceId.Value,
                cancellationToken);
            if (byId is not null) return byId;
        }

        if (arguments.SelectedIndex.HasValue)
        {
            var fromContext = await ResolveFromLastSearchContextAsync(
                arguments.SelectedIndex.Value,
                sessionId,
                cancellationToken);
            if (fromContext is not null) return fromContext;
        }

        if (!string.IsNullOrWhiteSpace(arguments.PlaceName))
        {
            var runtime = await _tuning.GetRuntimeAsync(cancellationToken);
            var limit = runtime.ToolNameMatchLimit > 0 ? runtime.ToolNameMatchLimit : 5;
            var candidates = await _placeRepository.SearchByNameAsync(
                arguments.PlaceName,
                limit: limit,
                cancellationToken);
            var exact = candidates.FirstOrDefault(c =>
                string.Equals(c.Name, arguments.PlaceName, StringComparison.OrdinalIgnoreCase));
            return exact ?? (candidates.Count > 0 ? candidates[0] : null);
        }

        return await ResolveFromLastSearchContextAsync(
            index: 0,
            sessionId,
            cancellationToken);
    }

    private async Task<Places.Domain.Entities.Place?> ResolveFromLastSearchContextAsync(
        int index,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = await _lastSearchContextStore.GetAsync(
                sessionId,
                cancellationToken);
            if (context is null) return null;

            var ids = JsonSerializer.Deserialize<List<Guid>>(context.ResultPlaceIdsJson);
            if (ids is null || index < 0 || index >= ids.Count) return null;

            return await _placeRepository.GetByIdAsync(ids[index], cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to resolve place from LastSearchContext for session {SessionId}",
                sessionId);
            return null;
        }
    }

    private static string ComputeIdempotencyKey(Guid userId, string payloadJson)
    {
        var raw = $"{userId:N}:create_meeting:{payloadJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return EmailRegex.IsMatch(email.Trim());
    }
}
