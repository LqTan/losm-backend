using AgentCore.Application.Models;

namespace AgentCore.Application.Abstractions;

public interface IAgentModelClient
{
    Task<AgentModelResponse> SendAsync(
        IReadOnlyList<AgentModelMessage> messages,
        IReadOnlyCollection<IAgentTool> tools,
        CancellationToken cancellationToken = default
    );
}
