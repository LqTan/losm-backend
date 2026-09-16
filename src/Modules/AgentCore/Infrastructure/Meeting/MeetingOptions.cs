namespace AgentCore.Infrastructure.Meeting;

public sealed class MeetingOptions
{
    public int DefaultDurationMinutes { get; set; } = 60;
    public string CalendarTimeZone { get; set; } = "Asia/Ho_Chi_Minh";
    public bool AttachIcs { get; set; } = true;
}