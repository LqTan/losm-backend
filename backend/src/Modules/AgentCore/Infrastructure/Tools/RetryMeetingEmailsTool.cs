using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.RetryMeetingEmails;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using Configuration.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class RetryMeetingEmailsTool
    : AgentTool<RetryMeetingEmailsToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPendingActionStore _pendingActionStore;
    private readonly IAgentExecutionContext _executionContext;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;

    public RetryMeetingEmailsTool(
        IPendingActionStore pendingActionStore,
        IAgentExecutionContext executionContext,
        Configuration.Application.Abstractions.ITuningProvider tuning)
        : base(tuning, "retry_meeting_emails")
    {
        _pendingActionStore = pendingActionStore;
        _executionContext = executionContext;
        _tuning = tuning;
    }

    public override string Name => "retry_meeting_emails";

    public override string Description =>
        "Retry the email invitation step of a previously partially-failed meeting. " +
        "Accepts the originalActionId (the create_meeting action id) and creates a " +
        "new pending 'retry_meeting_emails' action that, once confirmed, will " +
        "call n8n to resend invitations. " +
        "Use when a create_meeting completed with calendarCreated=true but emailsSent=false.";

    public override ToolKind Kind => ToolKind.WriteRequiresConfirmation;

    protected override async Task<string> ExecuteAsync(
        RetryMeetingEmailsToolArguments arguments,
        CancellationToken cancellationToken)
    {
        var userId = _executionContext.UserId;
        var sessionId = _executionContext.SessionId;

        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Current user is not available.");
        }
        if (!sessionId.HasValue || sessionId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Current session is not available.");
        }

        if (arguments.OriginalActionId == Guid.Empty)
        {
            throw new ArgumentException(
                "OriginalActionId is required.",
                nameof(arguments.OriginalActionId));
        }

        var original = await _pendingActionStore.GetAsync(
            arguments.OriginalActionId,
            cancellationToken);

        if (original is null || original.UserId != userId.Value)
        {
            throw new KeyNotFoundException(
                $"Original meeting action '{arguments.OriginalActionId}' was not found.");
        }

        if (original.ActionType != "create_meeting")
        {
            throw new InvalidOperationException(
                $"Action '{arguments.OriginalActionId}' is not a create_meeting action.");
        }

        if (original.Status != PendingAgentActionStatus.PartiallyFailed &&
            original.Status != PendingAgentActionStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Action '{arguments.OriginalActionId}' status is '{original.Status}'. " +
                "Retry is only allowed when status is PartiallyFailed or Failed.");
        }

        var confirmationId = Guid.NewGuid();
        var actionId = Guid.NewGuid();

        var payload = new
        {
            originalActionId = original.Id,
            originalConfirmationId = original.ConfirmationId,
            sessionId = sessionId.Value,
            userId = userId.Value
        };

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        var retryAction = new PendingAgentAction(
            id: actionId,
            sessionId: sessionId.Value,
            userId: userId.Value,
            actionType: "retry_meeting_emails",
            payloadJson: payloadJson,
            description:
                $"Gửi lại email mời cho cuộc hẹn '{original.Description}'",
            confirmationId: confirmationId
        );

        await _pendingActionStore.AddAsync(retryAction, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "awaiting_confirmation",
            actionId = retryAction.Id,
            confirmationId = confirmationId,
            action = "retry_meeting_emails",
            originalActionId = original.Id,
            message = "User must confirm before email invitations are resent."
        }, JsonOptions);
    }
}
