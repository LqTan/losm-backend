using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using Places.Application.SavedPlaces.Commands.SavePlace;
using ReviewPlacePayload = System.Text.Json.JsonElement;

namespace AgentCore.Infrastructure.Dispatchers;

public sealed class SavePlaceDispatcher : IAgentActionDispatcher
{
    public string ActionType => "save_place";

    private readonly SavePlaceHandler _savePlaceHandler;

    public SavePlaceDispatcher(SavePlaceHandler savePlaceHandler)
    {
        _savePlaceHandler = savePlaceHandler;
    }

    public async Task<DispatchResult> DispatchAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(action.PayloadJson);
            var root = doc.RootElement;

            var placeId = root.GetProperty("placeId").GetGuid();
            var note = root.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null
                ? n.GetString()
                : null;

            var result = await _savePlaceHandler.HandleAsync(
                new SavePlaceCommand(action.UserId, placeId, note),
                cancellationToken);

            var payload = JsonSerializer.Serialize(new
            {
                result = "saved",
                savedPlaceId = result.SavedPlaceId,
                placeId = result.PlaceId
            });

            return DispatchResult.Ok(payload);
        }
        catch (Exception ex)
        {
            return DispatchResult.Fail(ex.Message);
        }
    }
}
