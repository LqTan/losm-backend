using System.Text.Json;
using AgentCore.Application.Models;

namespace AgentCore.Application.Abstractions;

public interface IMeetingAutomationClient
{
    Task<MeetingAutomationResult> TriggerMeetingAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default);

    Task<MeetingAutomationResult> ResendInvitationsAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default);
}
