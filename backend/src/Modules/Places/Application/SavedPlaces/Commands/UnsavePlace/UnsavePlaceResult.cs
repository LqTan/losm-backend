namespace Places.Application.SavedPlaces.Commands.UnsavePlace;

public sealed record UnsavePlaceResult(
    Guid UserId,
    Guid PlaceId,
    bool Removed
);
