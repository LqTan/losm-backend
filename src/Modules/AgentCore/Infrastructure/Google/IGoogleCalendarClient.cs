namespace AgentCore.Infrastructure.Google;

public sealed record CalendarEventRequest(
    string Title,
    string? Description,
    DateTime StartAtUtc,
    int DurationMinutes,
    string TimeZone,
    string? LocationAddress,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> AttendeeEmails
);

public sealed record CalendarEventResult(
    bool Created,
    string? EventId,
    string? HtmlLink,
    string? Error
);

public interface IGoogleCalendarClient
{
    Task<CalendarEventResult> CreateEventAsync(
        Guid userId,
        CalendarEventRequest request,
        CancellationToken cancellationToken = default);
}