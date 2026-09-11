namespace AgentCore.Application.Tools.SavePlace;

public sealed record SavePlaceToolArguments(
    string PlaceName,
    string? Note = null
);
