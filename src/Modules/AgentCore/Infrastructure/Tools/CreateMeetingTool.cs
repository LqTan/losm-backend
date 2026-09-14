using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.CreateMeeting;
using Configuration.Application.Abstractions;
using Places.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class CreateMeetingTool
    : AgentTool<CreateMeetingToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPlaceRepository _placeRepository;
    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentExecutionContext _executionContext;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;

    public CreateMeetingTool(
        IPlaceRepository placeRepository,
        IPendingActionStore pendingActionStore,
        IAgentExecutionContext executionContext,
        Configuration.Application.Abstractions.ITuningProvider tuning)
        : base(tuning, "create_meeting")
    {
        _placeRepository = placeRepository;
        _pendingActionStore = pendingActionStore;
        _executionContext = executionContext;
        _tuning = tuning;
    }

    public override string Name => "create_meeting";

    public override string Description =>
        "Create a meeting at a saved place, invite attendees, and create a Google Calendar event. " +
        "Returns a confirmation draft that the user must approve in the UI before it is executed. " +
        "Call this whenever the user asks to schedule, set up, or arrange a meeting.";

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
        if (string.IsNullOrWhiteSpace(arguments.PlaceName))
        {
            throw new ArgumentException("PlaceName is required.", nameof(arguments.PlaceName));
        }
        if (arguments.AttendeeEmails is null || arguments.AttendeeEmails.Length == 0)
        {
            throw new ArgumentException("At least one attendee email is required.",
                nameof(arguments.AttendeeEmails));
        }

        var candidates = await _placeRepository.SearchByNameAsync(
            arguments.PlaceName, limit: 5, cancellationToken);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No place matching '{arguments.PlaceName}' was found in the database.");
        }

        var exact = candidates.FirstOrDefault(c =>
            string.Equals(c.Name, arguments.PlaceName, StringComparison.OrdinalIgnoreCase));
        var place = exact ?? candidates[0];

        var confirmationId = Guid.NewGuid();
        var actionId = Guid.NewGuid();

        var payload = new
        {
            placeId = place.Id,
            placeName = place.Name,
            title = arguments.Title,
            startAt = arguments.StartAt,
            durationMinutes = arguments.DurationMinutes,
            attendees = arguments.AttendeeEmails,
            note = arguments.Note
        };

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var idempotencyKey = ComputeIdempotencyKey(userId.Value, payloadJson);

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

    private static string ComputeIdempotencyKey(Guid userId, string payloadJson)
    {
        var raw = $"{userId:N}:create_meeting:{payloadJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
