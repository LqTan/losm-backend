using Agent.Domain;

namespace Agent.Application.Abstractions;

public interface IConversationMemory
{
    void AddTurn(string sessionId, ConversationTurn turn);

    IReadOnlyList<ConversationTurn> GetRecentTurns(
        string sessionId,
        int maximumTurns = 10);
}