namespace AgentCore.Application.Abstractions;

public interface IAgentToolRegistry
{
    IReadOnlyCollection<IAgentTool> GetAll();
    IAgentTool GetRequired(string name);
}
