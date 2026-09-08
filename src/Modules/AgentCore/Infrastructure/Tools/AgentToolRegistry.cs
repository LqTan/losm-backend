using AgentCore.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class ArgentToolRegistry : IAgentToolRegistry
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools;
    public ArgentToolRegistry(
        IEnumerable<IAgentTool> tools
    )
    {
        var toolList = tools.ToList();

        var duplicate = toolList
            .GroupBy(x => x.Name)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate agent tool: {duplicate.Key}"
            );
        }

        _tools = toolList.ToDictionary(
            x => x.Name,
            StringComparer.Ordinal
        );
    }

    public IReadOnlyCollection<IAgentTool> GetAll()
    {
        return _tools.Values.ToArray();
    }

    public IAgentTool GetRequired(string name)
    {
        if (_tools.TryGetValue(name, out var tool))
        {
            return tool;
        }

        throw new KeyNotFoundException(
            $"Agent tool '{name}' was not found."
        );
    }
}
