namespace AgentCore.Application.Agent.Commands.RetryMeetingEmails;

public sealed record RetryMeetingEmailsCommand(
    Guid MeetingActionId,
    Guid UserId
);