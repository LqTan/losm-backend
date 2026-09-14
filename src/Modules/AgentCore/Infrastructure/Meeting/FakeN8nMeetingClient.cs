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

    public async Task<MeetingAutomationResult> TriggerAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "FakeN8n triggered with payload: {Payload}",
            payload.GetRawText());

        await Task.Delay(100, cancellationToken);

        return new MeetingAutomationResult(
            CalendarCreated: true,
            EmailsSent: true,
            CalendarEventId: $"fake-event-{Guid.NewGuid():N}",
            ErrorMessage: null
        );
    }
}
