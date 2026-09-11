using AgentCore.Application.Models;

namespace AgentCore.Application.Abstractions;

public interface IAgentRunner
{
    Task<AgentRunResult> RunAsync(
        string input,
        double latitude,
        double longitude,
        Guid? sessionId,
        Guid? userId,
        CancellationToken cancellationToken = default
    );

    Task<AgentRunResult> RunApprovedPlanAsync(
        string input,
        double latitude,
        double longitude,
        Guid? sessionId,
        Guid? userId,
        string approvedPlan,
        CancellationToken cancellationToken = default
    );

    Task<AgentRunResult> RunStreamedAsync(
        string input,
        double latitude,
        double longitude,
        Guid? sessionId,
        Guid? userId,
        IAgentEventSink sink,
        CancellationToken cancellationToken = default
    );
}
