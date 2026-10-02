namespace Places.Application.SavedPlaces.Commands.SavePlace;

public sealed record SavePlaceCommand(
    Guid UserId,
    Guid PlaceId,
    string? Note
);
