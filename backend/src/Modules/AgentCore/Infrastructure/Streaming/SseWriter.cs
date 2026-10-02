using System.Text;
using AgentCore.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace AgentCore.Infrastructure.Streaming;

public static class SseWriter
{
    public static async Task WriteSseStreamAsync(
        HttpResponse response,
        SseAgentEventSink sink,
        CancellationToken cancellationToken
    )
    {
        response.StatusCode = 200;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";

        var writer = response.BodyWriter;

        try
        {
            while (
                await sink.Reader.WaitToReadAsync(cancellationToken)
            )
            {
                while (sink.Reader.TryRead(out var evt))
                {
                    var bytes = Serialize(evt);
                    await writer.WriteAsync(bytes, cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected
        }
    }

    private static byte[] Serialize(SseEvent evt)
    {
        var sb = new StringBuilder(128);
        sb.Append("event: ").Append(evt.EventName).Append('\n');
        foreach (var line in evt.Data.Split('\n'))
        {
            sb.Append("data: ").Append(line).Append('\n');
        }
        sb.Append('\n');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
