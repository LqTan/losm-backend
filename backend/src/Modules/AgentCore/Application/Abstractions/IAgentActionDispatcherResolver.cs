namespace AgentCore.Application.Abstractions;

public interface IAgentActionDispatcherResolver
{
    IAgentActionDispatcher? Resolve(string actionType);
    IReadOnlyCollection<string> ListActionTypes();
}
