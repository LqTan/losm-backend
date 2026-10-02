namespace Places.Application.SavedPlaces.Commands.SavePlace;

public sealed record SavePlaceResult(
    Guid SavedPlaceId,
    Guid UserId,
    Guid PlaceId,
    string? Note,
    DateTime CreatedAt
);
