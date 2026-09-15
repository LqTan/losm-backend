using System.Net.Http.Json;
using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentCore.Infrastructure.Meeting;

public sealed class N8nMeetingClient : IMeetingAutomationClient
{
    private readonly HttpClient _httpClient;
    private readonly N8nMeetingClientOptions _options;
    private readonly ILogger<N8nMeetingClient> _logger;

    public N8nMeetingClient(
        HttpClient httpClient,
        IOptions<N8nMeetingClientOptions> options,
        ILogger<N8nMeetingClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<MeetingAutomationResult> TriggerMeetingAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        return InvokeWebhookAsync(
            _options.MeetingWebhookPath,
            payload,
            cancellationToken);
    }

    public Task<MeetingAutomationResult> ResendInvitationsAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        return InvokeWebhookAsync(
            _options.ResendInvitationsWebhookPath,
            payload,
            cancellationToken);
    }

    private async Task<MeetingAutomationResult> InvokeWebhookAsync(
        string path,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new MeetingAutomationResult(
                false, false, null,
                "Webhook path is not configured");
        }

        var url = path;

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new MeetingAutomationResult(false, false, null,
                    $"n8n returned {(int)response.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var calendarCreated = root.TryGetProperty("calendarCreated", out var c1) && c1.GetBoolean();
            var emailsSent = root.TryGetProperty("emailsSent", out var c2) && c2.GetBoolean();
            var eventId = root.TryGetProperty("calendarEventId", out var c3) && c3.ValueKind != JsonValueKind.Null
                ? c3.GetString()
                : null;

            return new MeetingAutomationResult(calendarCreated, emailsSent, eventId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "n8n webhook call failed for path {Path}", path);
            return new MeetingAutomationResult(false, false, null, ex.Message);
        }
    }
}
