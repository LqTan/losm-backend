using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;

namespace AgentCore.Infrastructure.Validation;

public sealed class GroundingValidator
    : IAgentResponseValidator
{
    private readonly IAgentModelClient _modelClient;

    public GroundingValidator(
        IAgentModelClient modelClient
    )
    {
        _modelClient = modelClient;
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
                result = x.Result
            })
            .ToList();

        if (evidence.Count == 0)
        {
            return answer;
        }

        var payload = JsonSerializer.Serialize(
            new
            {
                candidateAnswer = answer,
                evidence
            }
        );

        var messages = new List<AgentModelMessage>
        {
            new(
                AgentModelRole.System,
                GroundingInstructions.Load()
            ),

            new(
                AgentModelRole.User,
                payload
            )
        };

        var response = await _modelClient.SendAsync(
            messages,
            Array.Empty<IAgentTool>(),
            cancellationToken
        );

        if (response.HasToolCalls)
        {
            throw new InvalidOperationException(
                "Grounding validator cannot call tools."
            );
        }

        var validatedAnswer =
            response.AssistantMessage.Content;

        if (string.IsNullOrWhiteSpace(validatedAnswer))
        {
            throw new InvalidOperationException(
                "Grounding validator returned an empty response."
            );
        }

        return validatedAnswer;
    }
}
