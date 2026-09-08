using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AgentCore.Application.Abstractions;

namespace AgentCore.Application.Tools;

public abstract class AgentTool<TArguments> : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    public abstract string Name { get; }
    public abstract string Description { get; }
    public JsonNode ParametersSchema => JsonOptions.GetJsonSchemaAsNode(typeof(TArguments));
    public async Task<string> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default
    )
    {
        var typedArguments = arguments.Deserialize<TArguments>(JsonOptions);
        if (typedArguments is null)
        {
            throw new ArgumentException(
                $"Invalid arguments for tool '{Name}'.",
                nameof(arguments)
            );
        }
        return await ExecuteAsync(
            typedArguments,
            cancellationToken
        );
    }
    protected abstract Task<string> ExecuteAsync(
        TArguments arguments,
        CancellationToken cancellationToken
    );
}
