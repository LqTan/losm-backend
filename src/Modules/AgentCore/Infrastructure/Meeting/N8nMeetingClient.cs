using System.Net.Http.Json;
using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentCore.Infrastructure.Meeting;

public sealed class N8nMeetingClient : IMeetingAutomationClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<N8nMeetingClient> _logger;

    public N8nMeetingClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<N8nMeetingClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MeetingAutomationResult> TriggerAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["N8n:BaseUrl"];
        var path = _configuration["N8n:MeetingWebhookPath"] ?? "/webhook/meeting";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new MeetingAutomationResult(
                false, false, null,
                "N8n BaseUrl is not configured");
        }

        var url = baseUrl.TrimEnd('/') + path;

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
            _logger.LogError(ex, "n8n webhook call failed");
            return new MeetingAutomationResult(false, false, null, ex.Message);
        }
    }
}
