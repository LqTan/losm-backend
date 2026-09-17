using AgentCore.Application.Abstractions;

namespace AgentCore.Application.Agent.Queries.GetPendingActionById;

public sealed class GetPendingActionByIdHandler
{
    private readonly IPendingActionStore _pendingActionStore;

    public GetPendingActionByIdHandler(IPendingActionStore pendingActionStore)
    {
        _pendingActionStore = pendingActionStore;
    }

    public async Task<GetPendingActionByIdResult?> HandleAsync(
        GetPendingActionByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var action = await _pendingActionStore.GetAsync(
            query.ActionId,
            cancellationToken);

        if (action is null || action.UserId != query.UserId)
        {
            return null;
        }

        return new GetPendingActionByIdResult(
            action.Id,
            action.SessionId,
            action.ActionType,
            action.Status.ToString(),
            action.Description,
            action.ConfirmationId,
            action.PayloadJson,
            action.CreatedAt,
            action.ConfirmedAt,
            action.CompletedAt,
            action.Error);
    }
}
