using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using Microsoft.Extensions.Configuration;

namespace AgentCore.Infrastructure.Llm;

public sealed class MiniMaxAgentClient : IAgentModelClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public MiniMaxAgentClient(
        HttpClient httpClient,
        IConfiguration configuration
    )
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<AgentModelResponse> SendAsync(
        IReadOnlyList<AgentModelMessage> messages,
        IReadOnlyCollection<IAgentTool> tools,
        CancellationToken cancellationToken = default
    )
    {
        var apiKey = _configuration["Minimax:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Minimax API key is not configured."
            );
        }

        var model = _configuration["Minimax:Model"] ?? "Minimax-M3";

        var messagesWithSystem = InjectSystemPrompt(messages);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "chat/completions"
        );
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            apiKey
        );
        request.Content = JsonContent.Create(
            new
            {
                model,
                messages = messagesWithSystem.Select(MapMessage),
                tools = tools.Select(MapTool)
            }
        );

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken
        );
        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Minimax request failed with status " +
                $"{(int)response.StatusCode}: {responseBody}"
            );
        }

        using var document = JsonDocument.Parse(responseBody);
        var message = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");
        string? content = null;

        if (message.TryGetProperty("content", out var contentElement) &&
            contentElement.ValueKind != JsonValueKind.Null)
        {
            content = CleanContent(contentElement.GetString());
        }
        
        var toolCalls = ParseToolCalls(message);
        var assistantMessage = new AgentModelMessage(
            AgentModelRole.Assistant,
            content,
            toolCalls
        );

        return new AgentModelResponse(
            assistantMessage,
            toolCalls
        );
    }

    private static object MapMessage(
        AgentModelMessage message
    )
    {
        return message.Role switch
        {
            AgentModelRole.System => new
            {
                role = "system",
                content = message.Content
            },
            AgentModelRole.User => new
            {
                role = "user",
                content = message.Content
            },
            AgentModelRole.Assistant => new
            {
                role = "assistant",
                content = message.Content,
                tool_calls = message.ToolCalls?
                    .Select(toolCall => new
                    {
                        id = toolCall.Id,
                        type = "function",
                        function = new
                        {
                            name = toolCall.Name,
                            arguments = toolCall.ArgumentsJson
                        }
                    })
            },
            AgentModelRole.Tool => new
            {
                role = "tool",
                tool_call_id = message.ToolCallId,
                content = message.Content
            },
            _ => throw new InvalidOperationException(
                $"Unsupported model role: {message.Role}"
            )
        };
    }

    private static object MapTool(
        IAgentTool tool
    )
    {
        return new
        {
            type = "function",
            function = new
            {
                name = tool.Name,
                description = tool.Description,
                parameters = tool.ParametersSchema
            }
        };
    }

    private static IReadOnlyList<AgentModelToolCall> ParseToolCalls(
        JsonElement message
    )
    {
        if (!message.TryGetProperty(
            "tool_calls",
            out var toolCallsElement
        ) ||
        toolCallsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var toolCalls = new List<AgentModelToolCall>();
        foreach (var item in toolCallsElement.EnumerateArray())
        {
            var function = item.GetProperty("function");
            toolCalls.Add(
                new AgentModelToolCall(
                    item.GetProperty("id").GetString()
                        ?? throw new JsonException(
                            "Tool call id is missing."
                        ),
                    function.GetProperty("name").GetString()
                        ?? throw new JsonException(
                            "Tool name is missing."
                        ),
                    function.GetProperty("arguments").GetString()
                        ?? "{}"
                )
            );
        }
        return toolCalls;
    }

    private static string? CleanContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var stripped = Regex.Replace(
            content,
            @"<think>[\s\S]*?</think>\s*",
            string.Empty,
            RegexOptions.IgnoreCase
        );

        stripped = MarkdownToPlainText(stripped);

        return stripped.Trim();
    }

    private static IReadOnlyList<AgentModelMessage> InjectSystemPrompt(
        IReadOnlyList<AgentModelMessage> messages
    )
    {
        const string systemPrompt =
            "You answer in plain conversational prose addressed to the user. " +
            "Never use markdown formatting: no bold, no italics, no bullet " +
            "lists, no numbered lists, no pipe tables, no headings, no code " +
            "fences, no horizontal rules. Use natural sentences and simple " +
            "punctuation. Place names can still be emphasized with quotation " +
            "marks if needed. Do not output JSON unless explicitly asked. " +
            "Keep replies concise.";

        var existing = messages.FirstOrDefault();

        if (existing is not null &&
            existing.Role == AgentModelRole.System)
        {
            return messages;
        }

        var result = new List<AgentModelMessage>(messages.Count + 1)
        {
            new AgentModelMessage(
                AgentModelRole.System,
                systemPrompt
            )
        };
        result.AddRange(messages);
        return result;
    }

    private static string MarkdownToPlainText(string input)
    {
        var text = input;

        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = Regex.Replace(text, @"__(.+?)__", "$1");
        text = Regex.Replace(text, @"\*(.+?)\*", "$1");
        text = Regex.Replace(text, @"_(.+?)_", "$1");
        text = Regex.Replace(text, @"~~(.+?)~~", "$1");
        text = Regex.Replace(text, @"`([^`]+)`", "$1");

        text = Regex.Replace(
            text,
            @"(\n\|.+\|\s*\n\|[\s\-:|]+\|)([\s\S]*?(?=\n\n|\n[^\|]|\Z))",
            match =>
            {
                var rowsBlock = match.Value;
                var lines = rowsBlock
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries);

                var rows = lines
                    .Where(line => !Regex.IsMatch(line, @"^\|[\s\-:|]+\|$"))
                    .ToList();

                var converted = new List<string>();
                foreach (var row in rows)
                {
                    var cells = row
                        .Trim(' ', '|')
                        .Split('|')
                        .Select(c => c.Trim())
                        .Where(c => c.Length > 0)
                        .ToList();

                    if (cells.Count == 0)
                    {
                        continue;
                    }

                    converted.Add(string.Join(" — ", cells));
                }

                return converted.Count == 0
                    ? string.Empty
                    : string.Join("\n", converted) + "\n";
            }
        );

        text = Regex.Replace(text, @"^\s{0,3}#{1,6}\s+", "",
            RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*[-*+]\s+", "",
            RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*\d+\.\s+", "",
            RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*>\s?", "",
            RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*[-*_]{3,}\s*$", "",
            RegexOptions.Multiline);

        text = Regex.Replace(text, @"\\([\\`*_{}\[\]()#+\-.!])", "$1");

        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        text = Regex.Replace(text, @"[ \t]+\n", "\n");

        return text;
    }
}
