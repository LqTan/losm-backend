namespace Places.Application.SavedPlaces.Commands.UnsavePlace;

public sealed record UnsavePlaceCommand(
    Guid UserId,
    Guid PlaceId
);
