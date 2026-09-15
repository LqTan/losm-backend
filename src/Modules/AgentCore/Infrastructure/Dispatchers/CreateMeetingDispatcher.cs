using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;

namespace AgentCore.Infrastructure.Dispatchers;

public sealed class CreateMeetingDispatcher : IAgentActionDispatcher
{
    public string ActionType => "create_meeting";

    private readonly IMeetingAutomationClient _client;
    private readonly IPlaceRepository _placeRepository;
    private readonly ILogger<CreateMeetingDispatcher> _logger;

    public CreateMeetingDispatcher(
        IMeetingAutomationClient client,
        IPlaceRepository placeRepository,
        ILogger<CreateMeetingDispatcher> logger)
    {
        _client = client;
        _placeRepository = placeRepository;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(action.PayloadJson);
            var root = doc.RootElement;

            var placeId = root.TryGetProperty("placeId", out var pid)
                ? pid.GetGuid()
                : Guid.Empty;

            var place = placeId != Guid.Empty
                ? await _placeRepository.GetByIdAsync(placeId, cancellationToken)
                : null;

            var enriched = EnrichPayload(action, root, place);
            var payloadJson = JsonSerializer.Serialize(enriched);
            using var enrichedDoc = JsonDocument.Parse(payloadJson);

            var result = await _client.TriggerMeetingAsync(
                enrichedDoc.RootElement,
                cancellationToken);

            return MapResult(result, action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meeting dispatch failed for action {ActionId}", action.Id);
            return DispatchResult.Fail(ex.Message);
        }
    }

    private static Dictionary<string, object?> EnrichPayload(
        PendingAgentAction action,
        JsonElement root,
        Places.Domain.Entities.Place? place)
    {
        var payload = new Dictionary<string, object?>();

        CopyIfPresent(root, "title", payload, "title");
        CopyIfPresent(root, "purpose", payload, "purpose");
        CopyIfPresent(root, "startAt", payload, "startAt");
        CopyIfPresent(root, "durationMinutes", payload, "durationMinutes");
        CopyIfPresent(root, "attendees", payload, "attendees");
        CopyIfPresent(root, "note", payload, "note");

        if (place is not null)
        {
            payload["placeId"] = place.Id;
            payload["placeName"] = place.Name;
            payload["address"] = place.Address;
            payload["latitude"] = place.Latitude;
            payload["longitude"] = place.Longitude;
        }
        else
        {
            CopyIfPresent(root, "placeId", payload, "placeId");
            CopyIfPresent(root, "placeName", payload, "placeName");
        }

        payload["sessionId"] = action.SessionId;
        payload["userId"] = action.UserId;
        payload["confirmationId"] = action.ConfirmationId;
        payload["idempotencyKey"] = action.IdempotencyKey;

        return payload;
    }

    private static void CopyIfPresent(
        JsonElement source,
        string name,
        Dictionary<string, object?> target,
        string targetName)
    {
        if (!source.TryGetProperty(name, out var element)) return;
        if (element.ValueKind == JsonValueKind.Null) return;
        target[targetName] = JsonSerializer.Deserialize<object?>(element.GetRawText());
    }

    private static DispatchResult MapResult(
        MeetingAutomationResult result,
        PendingAgentAction action)
    {
        var payload = JsonSerializer.Serialize(new
        {
            calendarCreated = result.CalendarCreated,
            emailsSent = result.EmailsSent,
            calendarEventId = result.CalendarEventId
        });

        if (result.CalendarCreated && result.EmailsSent)
        {
            return DispatchResult.Ok(payload);
        }

        if (result.CalendarCreated && !result.EmailsSent)
        {
            return DispatchResult.Partial(
                payload,
                result.ErrorMessage ?? "Calendar created but email invitations failed.");
        }

        return DispatchResult.Fail(
            result.ErrorMessage ?? "Meeting automation failed.");
    }
}

public sealed class RetryMeetingEmailsDispatcher : IAgentActionDispatcher
{
    public string ActionType => "retry_meeting_emails";

    private readonly IMeetingAutomationClient _client;
    private readonly ILogger<RetryMeetingEmailsDispatcher> _logger;

    public RetryMeetingEmailsDispatcher(
        IMeetingAutomationClient client,
        ILogger<RetryMeetingEmailsDispatcher> logger)
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
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = action.SessionId,
                userId = action.UserId,
                confirmationId = action.ConfirmationId,
                idempotencyKey = action.IdempotencyKey,
                sourceActionId = TryParseOriginalActionId(action.PayloadJson)
            });

            using var doc = JsonDocument.Parse(payload);
            var result = await _client.ResendInvitationsAsync(
                doc.RootElement,
                cancellationToken);

            if (result.EmailsSent)
            {
                return DispatchResult.Ok(JsonSerializer.Serialize(new
                {
                    emailsSent = true
                }));
            }

            return DispatchResult.Fail(result.ErrorMessage ?? "Email resend failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email retry failed for action {ActionId}", action.Id);
            return DispatchResult.Fail(ex.Message);
        }
    }

    private static Guid? TryParseOriginalActionId(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("originalActionId", out var id) &&
                id.ValueKind == JsonValueKind.String)
            {
                var raw = id.GetString();
                if (Guid.TryParse(raw, out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch
        {
        }
        return null;
    }
}
