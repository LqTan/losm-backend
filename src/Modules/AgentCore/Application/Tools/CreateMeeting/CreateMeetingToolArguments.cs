namespace AgentCore.Application.Tools.CreateMeeting;

public sealed record CreateMeetingToolArguments(
    string Title,
    DateTime StartAt,
    int DurationMinutes,
    string[] AttendeeEmails,
    string? Purpose = null,
    string? Note = null,
    Guid? PlaceId = null,
    string? PlaceName = null,
    int? SelectedIndex = null
);
