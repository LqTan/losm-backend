namespace Places.Application.SavedPlaces.Queries.GetSavedPlacesByUser;

public sealed record GetSavedPlacesByUserResult(
    Guid SavedPlaceId,
    Guid PlaceId,
    string? PlaceName,
    string? PlaceAddress,
    string? Note,
    DateTime CreatedAt
);
