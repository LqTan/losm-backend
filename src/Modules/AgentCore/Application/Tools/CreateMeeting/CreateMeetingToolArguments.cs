namespace AgentCore.Application.Tools.CreateMeeting;

public sealed record CreateMeetingToolArguments(
    string Title,
    string PlaceName,
    DateTime StartAt,
    int DurationMinutes,
    string[] AttendeeEmails,
    string? Note = null
);
