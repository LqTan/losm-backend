using AgentCore.Domain.Entities;

namespace AgentCore.Application.Abstractions;

public interface IAgentPlanner
{
    Task<string> CreateAsync(
        string input,
        double latitude,
        double longitude,
        IReadOnlyCollection<AgentMessage> conversation,
        CancellationToken cancellationToken = default
    );
}
