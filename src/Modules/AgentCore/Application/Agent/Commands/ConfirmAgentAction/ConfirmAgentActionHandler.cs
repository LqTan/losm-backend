using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using Places.Application.SavedPlaces.Commands.SavePlace;
using Reviews.Application.Reviews.Commands.CreateReview;

namespace AgentCore.Application.Agent.Commands.ConfirmAgentAction;

public sealed class ConfirmAgentActionHandler
{
    private readonly IPendingActionStore _pendingActionStore;
    private readonly SavePlaceHandler _savePlaceHandler;
    private readonly CreateReviewHandler _createReviewHandler;

    public ConfirmAgentActionHandler(
        IPendingActionStore pendingActionStore,
        SavePlaceHandler savePlaceHandler,
        CreateReviewHandler createReviewHandler
    )
    {
        _pendingActionStore = pendingActionStore;
        _savePlaceHandler = savePlaceHandler;
        _createReviewHandler = createReviewHandler;
    }

    public async Task<ConfirmAgentActionResult> HandleAsync(
        ConfirmAgentActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var pending = _pendingActionStore.Remove(command.ActionId);

        if (pending is null)
        {
            throw new KeyNotFoundException(
                $"Pending action '{command.ActionId}' was not found " +
                "or has already been processed."
            );
        }

        if (pending.UserId != command.UserId)
        {
            throw new KeyNotFoundException(
                $"Pending action '{command.ActionId}' was not found " +
                "or has already been processed."
            );
        }

        var outcome = pending.Type switch
        {
            PendingAgentActionType.SavePlace =>
                await ConfirmSavePlaceAsync(pending, cancellationToken),

            PendingAgentActionType.CreateReview =>
                await ConfirmCreateReviewAsync(pending, cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unsupported pending action type: {pending.Type}"
            )
        };

        return new ConfirmAgentActionResult(outcome);
    }

    private async Task<ConfirmAgentActionOutcome> ConfirmSavePlaceAsync(
        PendingAgentAction pending,
        CancellationToken cancellationToken
    )
    {
        if (pending.PlaceId is null)
        {
            throw new InvalidOperationException(
                "Pending save place action is missing PlaceId."
            );
        }

        await _savePlaceHandler.HandleAsync(
            new SavePlaceCommand(
                pending.UserId,
                pending.PlaceId.Value,
                pending.Note
            ),
            cancellationToken
        );

        return new ConfirmAgentActionOutcome(
            pending.Id,
            PendingAgentActionType.SavePlace,
            "applied",
            $"Place '{pending.PlaceId}' was saved."
        );
    }

    private async Task<ConfirmAgentActionOutcome> ConfirmCreateReviewAsync(
        PendingAgentAction pending,
        CancellationToken cancellationToken
    )
    {
        if (pending.PlaceId is null || pending.Rating is null)
        {
            throw new InvalidOperationException(
                "Pending create review action is missing PlaceId or Rating."
            );
        }

        await _createReviewHandler.HandleAsync(
            new CreateReviewCommand(
                pending.PlaceId.Value,
                pending.UserId,
                pending.Rating.Value,
                pending.Comment
            )
        );

        return new ConfirmAgentActionOutcome(
            pending.Id,
            PendingAgentActionType.CreateReview,
            "applied",
            $"Review for place '{pending.PlaceId}' was saved."
        );
    }
}
