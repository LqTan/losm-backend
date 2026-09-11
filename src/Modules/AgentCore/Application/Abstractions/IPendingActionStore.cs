using AgentCore.Application.Models;

namespace AgentCore.Application.Abstractions;

public interface IPendingActionStore
{
    PendingAgentAction Add(PendingAgentAction action);

    IReadOnlyList<PendingAgentAction> GetBySession(Guid sessionId);

    PendingAgentAction? Remove(Guid actionId);
}
