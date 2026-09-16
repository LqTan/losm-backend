using AgentCore.Application.Abstractions;
using AgentCore.Domain.Enums;

namespace AgentCore.Application.Agent.Queries.GetMeetingsForUser;

public sealed class GetMeetingsForUserHandler
{
    private readonly IPendingActionStore _pendingActionStore;

    public GetMeetingsForUserHandler(IPendingActionStore pendingActionStore)
    {
        _pendingActionStore = pendingActionStore;
    }

    public async Task<IReadOnlyList<GetMeetingsForUserResult>> HandleAsync(
        GetMeetingsForUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var all = await _pendingActionStore.GetByUserAsync(
            query.UserId,
            cancellationToken);

        var meetingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "create_meeting",
            "retry_meeting_emails"
        };

        var meetings = all
            .Where(a => meetingTypes.Contains(a.ActionType))
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        var now = DateTime.UtcNow;

        IEnumerable<Domain.Entities.PendingAgentAction> filtered = query.Scope switch
        {
            MeetingScope.Upcoming => meetings.Where(m =>
                m.Status == PendingAgentActionStatus.Pending ||
                (m.Status == PendingAgentActionStatus.Completed &&
                 ExtractStartAt(m) is { } start && start >= now)),
            MeetingScope.Past => meetings.Where(m =>
                m.Status == PendingAgentActionStatus.Completed &&
                ExtractStartAt(m) is { } start && start < now),
            _ => meetings
        };

        var limited = query.Limit is > 0
            ? filtered.Take(query.Limit.Value).ToList()
            : filtered.ToList();

        return limited.Select(Map).ToList();
    }

    private static DateTime? ExtractStartAt(Domain.Entities.PendingAgentAction action)
    {
        var payload = MeetingPayload.Parse(action.PayloadJson);
        return payload?.StartAt;
    }

    private static GetMeetingsForUserResult Map(
        Domain.Entities.PendingAgentAction action)
    {
        var payload = MeetingPayload.Parse(action.PayloadJson);
        var result = MeetingResultJson.Parse(action.ResultJson);

        return new GetMeetingsForUserResult(
            action.Id,
            action.SessionId,
            action.ActionType,
            action.Status.ToString(),
            action.CreatedAt,
            action.ConfirmedAt,
            action.CompletedAt,
            payload?.Title ?? payload?.PlaceName ?? "(không có tiêu đề)",
            payload?.Purpose,
            payload?.Note,
            payload?.StartAt,
            payload?.DurationMinutes,
            payload?.PlaceId,
            payload?.PlaceName,
            null,
            payload?.Attendees is null
                ? Array.Empty<string>()
                : payload.Attendees.ToArray(),
            action.Error,
            result?.CalendarCreated ?? false,
            result?.EmailsSent ?? false);
    }
}