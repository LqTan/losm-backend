using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using Microsoft.Extensions.Logging;

namespace AgentCore.Infrastructure.Meeting;

public sealed class FakeN8nMeetingClient : IMeetingAutomationClient
{
    private readonly ILogger<FakeN8nMeetingClient> _logger;

    public FakeN8nMeetingClient(ILogger<FakeN8nMeetingClient> logger)
    {
        _logger = logger;
    }

    public async Task<MeetingAutomationResult> TriggerMeetingAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "FakeN8n is enabled. This client must NEVER be used in production. Payload: {Payload}",
            payload.GetRawText());

        await Task.Delay(100, cancellationToken);

        return new MeetingAutomationResult(
            CalendarCreated: true,
            EmailsSent: true,
            CalendarEventId: $"fake-event-{Guid.NewGuid():N}",
            ErrorMessage: null
        );
    }

    public async Task<MeetingAutomationResult> ResendInvitationsAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "FakeN8n resend-invitations. Payload: {Payload}",
            payload.GetRawText());

        await Task.Delay(100, cancellationToken);

        return new MeetingAutomationResult(
            CalendarCreated: true,
            EmailsSent: true,
            CalendarEventId: null,
            ErrorMessage: null
        );
    }
}
