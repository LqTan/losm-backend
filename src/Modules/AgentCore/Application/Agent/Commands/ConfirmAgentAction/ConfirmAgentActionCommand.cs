using AgentCore.Application.Models;

namespace AgentCore.Application.Agent.Commands.ConfirmAgentAction;

public sealed record ConfirmAgentActionCommand(
    Guid ActionId,
    Guid UserId
);

public sealed record ConfirmAgentActionResult(
    ConfirmAgentActionOutcome Outcome
);
