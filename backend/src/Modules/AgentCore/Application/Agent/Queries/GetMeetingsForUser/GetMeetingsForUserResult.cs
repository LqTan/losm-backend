using System.Text.Json;

namespace AgentCore.Application.Agent.Queries.GetMeetingsForUser;

public sealed record GetMeetingsForUserResult(
    Guid ActionId,
    Guid SessionId,
    string ActionType,
    string Status,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt,
    string Title,
    string? Purpose,
    string? Note,
    DateTime? StartAt,
    int? DurationMinutes,
    Guid? PlaceId,
    string? PlaceName,
    string? PlaceAddress,
    IReadOnlyList<string> AttendeeEmails,
    string? Error,
    bool CalendarCreated,
    bool EmailsSent,
    double? Latitude = null,
    double? Longitude = null
);

public sealed record MeetingPayload(
    Guid? PlaceId = null,
    string? PlaceName = null,
    string? Address = null,
    double? Latitude = null,
    double? Longitude = null,
    string? Title = null,
    string? Purpose = null,
    string? Note = null,
    DateTime? StartAt = null,
    int? DurationMinutes = null,
    IReadOnlyList<string>? Attendees = null
)
{
    public static MeetingPayload? Parse(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MeetingPayload>(
                payloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return null;
        }
    }
}

public sealed record MeetingResultJson(
    bool? CalendarCreated,
    bool? EmailsSent
)
{
    public static MeetingResultJson? Parse(string? resultJson)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MeetingResultJson>(
                resultJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return null;
        }
    }
}