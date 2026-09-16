using AgentCore.Application.Abstractions;
using AgentCore.Domain.Enums;

namespace AgentCore.Application.Agent.Queries.GetPendingActionsForUser;

public sealed class GetPendingActionsForUserHandler
{
    private readonly IPendingActionStore _pendingActionStore;

    public GetPendingActionsForUserHandler(IPendingActionStore pendingActionStore)
    {
        _pendingActionStore = pendingActionStore;
    }

    public async Task<IReadOnlyList<GetPendingActionsForUserResult>> HandleAsync(
        GetPendingActionsForUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var all = await _pendingActionStore.GetByUserAsync(
            query.UserId,
            cancellationToken);

        var pendingStatuses = new HashSet<PendingAgentActionStatus>
        {
            PendingAgentActionStatus.Pending,
            PendingAgentActionStatus.Confirmed,
            PendingAgentActionStatus.Executing,
            PendingAgentActionStatus.PartiallyFailed
        };

        var pending = all
            .Where(a => pendingStatuses.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        if (query.Limit is > 0)
        {
            pending = pending.Take(query.Limit.Value).ToList();
        }

        return pending
            .Select(a => new GetPendingActionsForUserResult(
                a.Id,
                a.SessionId,
                a.ActionType,
                a.Status.ToString(),
                a.Description,
                a.PayloadJson,
                a.CreatedAt))
            .ToList();
    }
}