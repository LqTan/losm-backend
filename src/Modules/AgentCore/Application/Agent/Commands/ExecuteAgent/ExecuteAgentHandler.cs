using AgentCore.Application.Abstractions;

namespace AgentCore.Application.Agent.Commands.ExecuteAgent;

public sealed class ExecuteAgentHandler
{
    private readonly IAgentRunner _agentRunner;
    public ExecuteAgentHandler(
        IAgentRunner agentRunner
    )
    {
        _agentRunner = agentRunner;
    }

    public async Task<ExecuteAgentResult> HandleAsync(
        ExecuteAgentCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _agentRunner.RunAsync(
            command.Message,
            command.Latitude,
            command.Longitude,
            command.SessionId,
            command.UserId,
            cancellationToken
        );

        return new ExecuteAgentResult(
            result.SessionId,
            result.Answer,
            result.Steps,
            result.PendingActions,
            result.AttachedPlaces
        );
    }
}
