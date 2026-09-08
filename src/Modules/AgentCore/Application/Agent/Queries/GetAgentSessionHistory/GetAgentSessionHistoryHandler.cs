using AgentCore.Application.Abstractions;

namespace AgentCore.Application.Agent.Queries.GetAgentSessionHistory;

public sealed class GetAgentSessionHistoryHandler
{
    private readonly IAgentSessionRepository _sessionRepository;

    public GetAgentSessionHistoryHandler(
        IAgentSessionRepository sessionRepository
    )
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<GetAgentSessionHistoryResult> HandleAsync(
        GetAgentSessionHistoryQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var session = await _sessionRepository.GetByIdAsync(
            query.SessionId,
            cancellationToken
        );

        if (session is null || session.UserId != query.UserId)
        {
            throw new KeyNotFoundException(
                $"Agent session '{query.SessionId}' was not found."
            );
        }

        var messages = session.Messages
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AgentMessageHistoryResult(
                x.Id,
                x.Role.ToString(),
                x.Content,
                x.CreatedAt
            ))
            .ToList();

        var toolCalls = session.ToolCalls
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AgentToolCallHistoryResult(
                x.Id,
                x.ToolName,
                x.ArgumentsJson,
                x.ResultJson,
                x.Error,
                x.Status.ToString(),
                x.CreatedAt,
                x.CompletedAt
            ))
            .ToList();

        return new GetAgentSessionHistoryResult(
            session.Id,
            session.CreatedAt,
            session.UpdatedAt,
            messages,
            toolCalls
        );
    }
}
