using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using AgentCore.Infrastructure.Dispatchers;
using Microsoft.Extensions.Logging;

namespace AgentCore.Application.Agent.Commands.RetryMeetingEmails;

public sealed class RetryMeetingEmailsHandler
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentActionDispatcherResolver _dispatcherResolver;
    private readonly ILogger<RetryMeetingEmailsHandler> _logger;

    public RetryMeetingEmailsHandler(
        IPendingActionStore pendingActionStore,
        IAgentActionDispatcherResolver dispatcherResolver,
        ILogger<RetryMeetingEmailsHandler> logger)
    {
        _pendingActionStore = pendingActionStore;
        _dispatcherResolver = dispatcherResolver;
        _logger = logger;
    }

    public async Task<RetryMeetingEmailsResult?> HandleAsync(
        RetryMeetingEmailsCommand command,
        CancellationToken cancellationToken = default)
    {
        var original = await _pendingActionStore.GetAsync(
            command.MeetingActionId,
            cancellationToken);

        if (original is null || original.UserId != command.UserId)
        {
            return null;
        }

        if (!string.Equals(original.ActionType, "create_meeting",
                StringComparison.OrdinalIgnoreCase))
        {
            return new RetryMeetingEmailsResult(
                original.Id,
                Guid.Empty,
                "InvalidActionType",
                false,
                "Action này không phải create_meeting.");
        }

        if (original.Status != PendingAgentActionStatus.PartiallyFailed &&
            original.Status != PendingAgentActionStatus.Failed)
        {
            return new RetryMeetingEmailsResult(
                original.Id,
                Guid.Empty,
                "InvalidStatus",
                false,
                "Chỉ retry được meeting ở trạng thái PartiallyFailed hoặc Failed.");
        }

        using var doc = JsonDocument.Parse(original.PayloadJson);
        var root = doc.RootElement.Clone();

        var dispatcher = _dispatcherResolver.Resolve(original.ActionType);
        if (dispatcher is null)
        {
            return new RetryMeetingEmailsResult(
                original.Id,
                Guid.Empty,
                "NoDispatcher",
                false,
                "Không tìm thấy dispatcher cho create_meeting.");
        }

        var retryAction = new PendingAgentAction(
            Guid.NewGuid(),
            original.SessionId,
            original.UserId,
            "retry_meeting_emails",
            JsonSerializer.Serialize(new
            {
                originalActionId = original.Id
            }, JsonOptions),
            $"Retry email cho meeting {original.Id}",
            confirmationId: Guid.NewGuid(),
            idempotencyKey: Guid.NewGuid().ToString());

        retryAction.Confirm(retryAction.ConfirmationId ?? Guid.NewGuid());

        await _pendingActionStore.AddAsync(retryAction, cancellationToken);

        var dispatchResult = await dispatcher.DispatchAsync(retryAction, cancellationToken);

        if (dispatchResult.Succeeded)
        {
            retryAction.Complete(dispatchResult.ResultJson);
        }
        else if (!string.IsNullOrEmpty(dispatchResult.Error) &&
                 dispatchResult.ResultJson != "{}")
        {
            retryAction.PartiallyFail(dispatchResult.ResultJson, dispatchResult.Error);
        }
        else
        {
            retryAction.Fail(dispatchResult.Error ?? "Retry failed.");
        }

        await _pendingActionStore.UpdateAsync(retryAction, cancellationToken);

        _logger.LogInformation(
            "Retry meeting emails. Original={OriginalAction}, Retry={RetryAction}, Status={Status}",
            original.Id,
            retryAction.Id,
            retryAction.Status);

        var emailsSent = false;
        string? error = null;
        if (!string.IsNullOrEmpty(dispatchResult.ResultJson))
        {
            try
            {
                using var resultDoc = JsonDocument.Parse(dispatchResult.ResultJson);
                if (resultDoc.RootElement.TryGetProperty("emailsSent", out var es) &&
                    es.ValueKind == JsonValueKind.True)
                {
                    emailsSent = true;
                }
            }
            catch
            {
            }
        }

        if (!emailsSent)
        {
            error = dispatchResult.Error;
        }

        return new RetryMeetingEmailsResult(
            original.Id,
            retryAction.Id,
            retryAction.Status.ToString(),
            emailsSent,
            error);
    }
}