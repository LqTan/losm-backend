using AgentCore.Application.Models;

namespace AgentCore.Application.Abstractions;

public interface IAgentResponseValidator
{
    Task<string> ValidateAsync(
        string answer,
        IReadOnlyList<AgentToolExecution> toolExecutions,
        CancellationToken cancellationToken = default
    );
}
