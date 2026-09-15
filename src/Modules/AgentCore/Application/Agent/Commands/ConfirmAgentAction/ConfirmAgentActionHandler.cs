using System.Text.Json;
using System.Text.RegularExpressions;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using AgentCore.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AgentCore.Application.Agent.Commands.ConfirmAgentAction;

public sealed class ConfirmAgentActionHandler
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentActionDispatcherResolver _dispatcherResolver;
    private readonly ILogger<ConfirmAgentActionHandler> _logger;

    public ConfirmAgentActionHandler(
        IPendingActionStore pendingActionStore,
        IAgentActionDispatcherResolver dispatcherResolver,
        ILogger<ConfirmAgentActionHandler> logger)
    {
        _pendingActionStore = pendingActionStore;
        _dispatcherResolver = dispatcherResolver;
        _logger = logger;
    }

    public async Task<ConfirmAgentActionResult> HandleAsync(
        ConfirmAgentActionCommand command,
        CancellationToken cancellationToken = default)
    {
        var pending = await _pendingActionStore.GetAsync(
            command.ActionId, cancellationToken);

        if (pending is null)
        {
            throw new KeyNotFoundException(
                $"Pending action '{command.ActionId}' was not found " +
                "or has already been processed.");
        }

        if (pending.UserId != command.UserId)
        {
            throw new KeyNotFoundException(
                $"Pending action '{command.ActionId}' was not found " +
                "or has already been processed.");
        }

        if (pending.Status == PendingAgentActionStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"Pending action '{command.ActionId}' was cancelled.");
        }

        ValidatePayloadEmails(pending);

        if (pending.Status == PendingAgentActionStatus.Completed)
        {
            return Replay(
                pending,
                "Action already completed; returning previous result.");
        }

        if (pending.Status == PendingAgentActionStatus.PartiallyFailed)
        {
            return Replay(
                pending,
                "Action previously partially failed; returning previous result. Use the dedicated retry tool to resume the failed step.");
        }

        if (pending.Status == PendingAgentActionStatus.Failed)
        {
            return Replay(
                pending,
                "Action previously failed; returning previous result.");
        }

        if (pending.Status == PendingAgentActionStatus.Executing)
        {
            return Replay(
                pending,
                "Action is currently executing; please retry shortly.");
        }

        var confirmationId = pending.ConfirmationId ?? Guid.NewGuid();
        pending.Confirm(confirmationId);
        await _pendingActionStore.UpdateAsync(pending, cancellationToken);

        pending.StartExecution();
        await _pendingActionStore.UpdateAsync(pending, cancellationToken);

        var dispatcher = _dispatcherResolver.Resolve(pending.ActionType);

        if (dispatcher is null)
        {
            pending.Fail($"No dispatcher registered for action '{pending.ActionType}'.");
            await _pendingActionStore.UpdateAsync(pending, cancellationToken);
            return new ConfirmAgentActionResult(new ConfirmAgentActionOutcome(
                pending.Id,
                pending.ActionType,
                pending.Status.ToString(),
                $"No dispatcher registered for action '{pending.ActionType}'."
            ));
        }

        var result = await dispatcher.DispatchAsync(pending, cancellationToken);

        if (result.Succeeded)
        {
            pending.Complete(result.ResultJson);
        }
        else if (!string.IsNullOrEmpty(result.ResultJson) &&
                 result.ResultJson != "{}")
        {
            pending.PartiallyFail(result.ResultJson,
                result.Error ?? "Partial failure");
        }
        else
        {
            pending.Fail(result.Error ?? "Unknown error");
        }

        await _pendingActionStore.UpdateAsync(pending, cancellationToken);

        var detail = result.Succeeded
            ? $"Action '{pending.ActionType}' applied."
            : (result.Error ?? "Action failed.");

        return new ConfirmAgentActionResult(new ConfirmAgentActionOutcome(
            pending.Id,
            pending.ActionType,
            pending.Status.ToString(),
            detail));
    }

    private static ConfirmAgentActionResult Replay(
        AgentCore.Domain.Entities.PendingAgentAction pending,
        string detail) =>
        new(new ConfirmAgentActionOutcome(
            pending.Id,
            pending.ActionType,
            pending.Status.ToString(),
            detail));

    private static void ValidatePayloadEmails(
        AgentCore.Domain.Entities.PendingAgentAction pending)
    {
        if (pending.ActionType != "create_meeting") return;

        try
        {
            using var doc = JsonDocument.Parse(pending.PayloadJson);
            if (!doc.RootElement.TryGetProperty("attendees", out var attendees) ||
                attendees.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var invalid = new List<string>();
            foreach (var item in attendees.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                var email = item.GetString();
                if (string.IsNullOrWhiteSpace(email) ||
                    !EmailRegex.IsMatch(email.Trim()))
                {
                    invalid.Add(email ?? "(empty)");
                }
            }

            if (invalid.Count > 0)
            {
                throw new ArgumentException(
                    $"Invalid email format in pending action: {string.Join(", ", invalid)}. " +
                    "Ask the user to provide valid email addresses before confirming.");
            }
        }
        catch (JsonException)
        {
        }
    }
}
