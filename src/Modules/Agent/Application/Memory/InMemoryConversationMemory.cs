using System.Collections.Concurrent;
using Agent.Application.Abstractions;
using Agent.Domain;

namespace Agent.Application.Memory;

public sealed class InMemoryConversationMemory : IConversationMemory
{
    private readonly ConcurrentDictionary<string, SessionConversation> _sessions = new();

    public void AddTurn(string sessionId, ConversationTurn turn)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException(
                "SessionId không được để trống.",
                nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(turn);

        var session = _sessions.GetOrAdd(sessionId, _ => new SessionConversation());

        lock (session.SyncRoot)
        {
            session.Turns.Add(turn);
        }
    }

    public IReadOnlyList<ConversationTurn> GetRecentTurns(
        string sessionId,
        int maximumTurns = 10)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException(
                "SessionId không được để trống.",
                nameof(sessionId));
        }

        if (maximumTurns <= 0)
        {
            return [];
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return [];
        }

        lock (session.SyncRoot)
        {
            return session.Turns
                .TakeLast(maximumTurns)
                .ToList();
        }
    }

    private sealed class SessionConversation
    {
        public object SyncRoot { get; } = new();

        public List<ConversationTurn> Turns { get; } = [];
    }
}