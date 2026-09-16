namespace AgentCore.Application.Agent.Commands.RetryMeetingEmails;

public sealed record RetryMeetingEmailsResult(
    Guid OriginalActionId,
    Guid RetryActionId,
    string Status,
    bool EmailsSent,
    string? Error
);