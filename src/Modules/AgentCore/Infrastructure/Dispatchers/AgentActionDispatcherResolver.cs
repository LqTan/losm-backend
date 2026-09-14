using AgentCore.Application.Abstractions;

namespace AgentCore.Infrastructure.Dispatchers;

public sealed class AgentActionDispatcherResolver : IAgentActionDispatcherResolver
{
    private readonly IReadOnlyDictionary<string, IAgentActionDispatcher> _dispatchers;

    public AgentActionDispatcherResolver(IEnumerable<IAgentActionDispatcher> dispatchers)
    {
        _dispatchers = dispatchers.ToDictionary(
            d => d.ActionType,
            StringComparer.Ordinal);
    }

    public IAgentActionDispatcher? Resolve(string actionType)
    {
        return _dispatchers.TryGetValue(actionType, out var d) ? d : null;
    }

    public IReadOnlyCollection<string> ListActionTypes() => _dispatchers.Keys.ToList();
}
