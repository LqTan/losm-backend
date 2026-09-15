namespace AgentCore.Infrastructure.Meeting;

public sealed class N8nMeetingClientOptions
{
    public string MeetingWebhookPath { get; set; } = "/webhook/meeting";
    public string ResendInvitationsWebhookPath { get; set; } = "/webhook/meeting/resend-invitations";
}
