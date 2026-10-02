using AgentCore.Domain.Entities;

namespace AgentCore.Application.Abstractions;

public interface IAgentActionDispatcher
{
    string ActionType { get; }
    Task<DispatchResult> DispatchAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default);
}

public sealed record DispatchResult(
    bool Succeeded,
    string ResultJson,
    string? Error
)
{
    public static DispatchResult Ok(string resultJson) => new(true, resultJson, null);
    public static DispatchResult Fail(string error) => new(false, "{}", error);
    public static DispatchResult Partial(string resultJson, string error) =>
        new(false, resultJson, error);
}
