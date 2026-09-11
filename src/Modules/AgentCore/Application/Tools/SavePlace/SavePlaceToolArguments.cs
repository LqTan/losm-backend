namespace AgentCore.Application.Tools.SavePlace;

public sealed record SavePlaceToolArguments(
    Guid PlaceId,
    string? Note = null
);
