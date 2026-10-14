namespace AgentCore.Application.Tools.FlagInvalidPlace;

public sealed record FlagInvalidPlaceToolArguments(
    string? PlaceId,
    string? PlaceName,
    string Reason
);
