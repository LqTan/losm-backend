using AgentCore.Application.Abstractions;
using AgentCore.Domain.Enums;
using Places.Application.Abstractions;

namespace AgentCore.Application.Agent.Queries.GetMeetingsForUser;

public sealed class GetMeetingsForUserHandler
{
    private readonly IPendingActionStore _pendingActionStore;
    private readonly IPlaceRepository? _placeRepository;

    public GetMeetingsForUserHandler(
        IPendingActionStore pendingActionStore,
        IPlaceRepository? placeRepository = null)
    {
        _pendingActionStore = pendingActionStore;
        _placeRepository = placeRepository;
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

        var results = new List<GetMeetingsForUserResult>();
        foreach (var action in limited)
        {
            results.Add(await MapAsync(action, cancellationToken));
        }
        return results;
    }

    private static DateTime? ExtractStartAt(Domain.Entities.PendingAgentAction action)
    {
        var payload = MeetingPayload.Parse(action.PayloadJson);
        return payload?.StartAt;
    }

    private async Task<GetMeetingsForUserResult> MapAsync(
        Domain.Entities.PendingAgentAction action,
        CancellationToken cancellationToken)
    {
        var payload = MeetingPayload.Parse(action.PayloadJson);
        var result = MeetingResultJson.Parse(action.ResultJson);

        var lat = payload?.Latitude;
        var lng = payload?.Longitude;
        var address = payload?.Address;

        if ((!lat.HasValue || !lng.HasValue) && _placeRepository != null)
        {
            try
            {
                if (payload?.PlaceId.HasValue == true)
                {
                    var place = await _placeRepository.GetByIdAsync(payload.PlaceId.Value, cancellationToken);
                    if (place != null)
                    {
                        lat = place.Latitude;
                        lng = place.Longitude;
                        address ??= place.Address;
                    }
                }

                if ((!lat.HasValue || !lng.HasValue) && !string.IsNullOrWhiteSpace(payload?.PlaceName))
                {
                    var places = await _placeRepository.SearchByNameAsync(payload.PlaceName, 1, cancellationToken);
                    var matched = places.FirstOrDefault();
                    if (matched != null)
                    {
                        lat = matched.Latitude;
                        lng = matched.Longitude;
                        address ??= matched.Address;
                    }
                }
            }
            catch
            {
                // Fallback silently if lookup fails
            }
        }

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
            address,
            payload?.Attendees is null
                ? Array.Empty<string>()
                : payload.Attendees.ToArray(),
            action.Error,
            result?.CalendarCreated ?? false,
            result?.EmailsSent ?? false,
            lat,
            lng);
    }
}