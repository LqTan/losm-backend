using System.Text.Json;
using AgentCore.Application.Models;

namespace AgentCore.Application.Abstractions;

public interface IMeetingAutomationClient
{
    Task<MeetingAutomationResult> TriggerAsync(
        JsonElement payload,
        CancellationToken cancellationToken = default);
}
