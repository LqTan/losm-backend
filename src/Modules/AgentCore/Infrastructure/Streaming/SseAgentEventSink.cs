using System.Text.Json;
using System.Threading.Channels;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;

namespace AgentCore.Infrastructure.Streaming;

public sealed class SseAgentEventSink : IAgentEventSink
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

    private readonly Channel<SseEvent> _channel =
        Channel.CreateUnbounded<SseEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

    public ChannelReader<SseEvent> Reader => _channel.Reader;

    public async Task EmitStepAsync(
        AgentActivityStep step,
        CancellationToken cancellationToken
    )
    {
        await _channel.Writer.WriteAsync(
            new SseEvent("step", JsonSerializer.Serialize(step, JsonOptions)),
            cancellationToken
        );
    }

    public async Task EmitResultAsync(
        AgentRunResult result,
        CancellationToken cancellationToken
    )
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                sessionId = result.SessionId,
                answer = result.Answer,
                steps = result.Steps,
                pendingActions = result.PendingActions,
                attachedPlaces = result.AttachedPlaces
            },
            JsonOptions
        );
        await _channel.Writer.WriteAsync(
            new SseEvent("result", payload),
            cancellationToken
        );
    }

    public async Task EmitErrorAsync(
        string message,
        CancellationToken cancellationToken
    )
    {
        var payload = JsonSerializer.Serialize(
            new { message },
            JsonOptions
        );
        await _channel.Writer.WriteAsync(
            new SseEvent("error", payload),
            cancellationToken
        );
    }

    public void Complete() => _channel.Writer.TryComplete();
}

public readonly record struct SseEvent(string EventName, string Data);
