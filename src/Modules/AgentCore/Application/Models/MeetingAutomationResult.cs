namespace AgentCore.Application.Models;

public sealed record MeetingAutomationResult(
    bool CalendarCreated,
    bool EmailsSent,
    string? CalendarEventId,
    string? ErrorMessage
);
