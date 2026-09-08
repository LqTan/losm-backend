using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentCore.Application.Abstractions;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    JsonNode ParametersSchema { get; }
    Task<string> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default
    );
}
