using AgentCore.Application.Models;

namespace AgentCore.Application.Abstractions;

public interface IAgentEventSink
{
    Task EmitStepAsync(
        AgentActivityStep step,
        CancellationToken cancellationToken
    );

    Task EmitResultAsync(
        AgentRunResult result,
        CancellationToken cancellationToken
    );

    Task EmitErrorAsync(
        string message,
        CancellationToken cancellationToken
    );
}
