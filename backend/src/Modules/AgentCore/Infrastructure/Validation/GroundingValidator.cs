using AgentCore.Infrastructure.PromptsManagement;
using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using Microsoft.Extensions.Logging;

namespace AgentCore.Infrastructure.Validation;

public sealed class GroundingValidator
    : IAgentResponseValidator
{
    private readonly PromptProvider? _prompts;
    private readonly IAgentModelClient _modelClient;
    private readonly ILogger<GroundingValidator>? _logger;

    public GroundingValidator(
        IAgentModelClient modelClient,
        PromptProvider? prompts = null,
        ILogger<GroundingValidator>? logger = null
    )
    {
        _prompts = prompts;
        _modelClient = modelClient;
        _logger = logger;
    }

    public async Task<string> ValidateAsync(
        string answer,
        IReadOnlyList<AgentToolExecution> toolExecutions,
        CancellationToken cancellationToken = default
    )
    {
        var evidence = toolExecutions
            .Where(x => x.Succeeded)
            .Select(x => new
            {
                tool = x.ToolName,
                result = x.Result.Length > 2000
                    ? string.Concat(x.Result.AsSpan(0, 2000), "... [truncated]")
                    : x.Result
            })
            .ToList();

        if (evidence.Count == 0)
        {
            return answer;
        }

        try
        {
            var payload = JsonSerializer.Serialize(
                new
                {
                    candidateAnswer = answer,
                    evidence
                }
            );

            var systemPrompt = _prompts is null
                ? GroundingInstructions.Load()
                : await _prompts.ResolveAsync("grounding-system", GroundingInstructions.Load(), cancellationToken);

            var messages = new List<AgentModelMessage>
            {
                new(AgentModelRole.System, systemPrompt),
                new(AgentModelRole.User, payload)
            };

            var response = await _modelClient.SendAsync(
                messages,
                Array.Empty<IAgentTool>(),
                cancellationToken
            );

            if (response.HasToolCalls)
            {
                _logger?.LogWarning("Grounding validator returned tool calls. Falling back to candidate answer.");
                return answer;
            }

            var validatedAnswer = response.AssistantMessage.Content;

            if (string.IsNullOrWhiteSpace(validatedAnswer))
            {
                _logger?.LogWarning("Grounding validator returned empty response. Falling back to candidate answer.");
                return answer;
            }

            return validatedAnswer;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Grounding validation failed or timed out. Falling back to candidate answer.");
            return answer;
        }
    }
}
