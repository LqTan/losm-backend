using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using AgentCore.Application.Abstractions;
using Configuration.Application.Abstractions;

namespace AgentCore.Application.Tools;

public abstract class AgentTool<TArguments> : IAgentTool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly ITuningProvider? _tuning;
    private readonly string _toolName;

    protected AgentTool()
    {
        _toolName = string.Empty;
    }

    protected AgentTool(ITuningProvider tuning, string toolName)
    {
        _tuning = tuning;
        _toolName = toolName;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual ToolKind Kind => ToolKind.ReadOnly;
    public virtual bool SuppliesPlaces => false;

    public ToolDisplay Display
    {
        get
        {
            if (_tuning is null) return ToolDisplay.Default(Name);

            try
            {
                var all = _tuning.GetToolDisplaysAsync(default).GetAwaiter().GetResult();
                if (all.TryGetValue(_toolName, out var d))
                {
                    return new ToolDisplay(
                        d.DisplayName,
                        d.CallingMessage,
                        d.SuccessMessage,
                        d.FailedMessage,
                        d.PendingConfirmationMessage,
                        d.ErrorHintMessage);
                }
            }
            catch
            {
            }
            return ToolDisplay.Default(Name);
        }
    }

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
