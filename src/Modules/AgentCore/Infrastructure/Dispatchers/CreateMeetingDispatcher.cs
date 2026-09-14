using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using AgentCore.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentCore.Infrastructure.Dispatchers;

public sealed class CreateMeetingDispatcher : IAgentActionDispatcher
{
    public string ActionType => "create_meeting";

    private readonly IMeetingAutomationClient _client;
    private readonly ILogger<CreateMeetingDispatcher> _logger;

    public CreateMeetingDispatcher(
        IMeetingAutomationClient client,
        ILogger<CreateMeetingDispatcher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(action.PayloadJson);
            var result = await _client.TriggerAsync(doc.RootElement, cancellationToken);

            if (result.CalendarCreated && result.EmailsSent)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    calendarCreated = true,
                    emailsSent = true,
                    calendarEventId = result.CalendarEventId
                });
                return DispatchResult.Ok(payload);
            }

            if (result.CalendarCreated && !result.EmailsSent)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    calendarCreated = true,
                    emailsSent = false,
                    calendarEventId = result.CalendarEventId
                });
                return DispatchResult.Partial(
                    payload,
                    result.ErrorMessage ?? "Calendar created but email invitations failed.");
            }

            return DispatchResult.Fail(
                result.ErrorMessage ?? "Meeting automation failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meeting dispatch failed for action {ActionId}", action.Id);
            return DispatchResult.Fail(ex.Message);
        }
    }
}
