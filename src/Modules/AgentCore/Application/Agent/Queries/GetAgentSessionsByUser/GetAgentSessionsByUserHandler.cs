using AgentCore.Application.Abstractions;

namespace AgentCore.Application.Agent.Queries.GetAgentSessionsByUser;

public sealed class GetAgentSessionsByUserHandler
{
    private readonly IAgentSessionRepository _sessionRepository;

    public GetAgentSessionsByUserHandler(
        IAgentSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<IReadOnlyList<GetAgentSessionsByUserResult>> HandleAsync(
        GetAgentSessionsByUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetListByUserAsync(
            query.UserId,
            query.Limit,
            cancellationToken);

        return sessions
            .Select(s => new GetAgentSessionsByUserResult(
                s.Id,
                BuildTitle(s),
                s.CreatedAt,
                s.UpdatedAt))
            .ToList();
    }

    private static string BuildTitle(Domain.Entities.AgentSession s)
    {
        var firstUserMessage = s.Messages
            .Where(m => m.Role == Domain.Enums.AgentMessageRole.User)
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Content)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(firstUserMessage))
        {
            var trimmed = firstUserMessage.Trim();
            return trimmed.Length <= 60 ? trimmed : trimmed[..60] + "…";
        }

        return "Cuộc trò chuyện mới";
    }
}
