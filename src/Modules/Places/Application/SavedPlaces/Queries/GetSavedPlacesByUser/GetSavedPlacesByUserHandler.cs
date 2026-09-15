using Places.Application.Abstractions;
using Places.Domain.Entities;

namespace Places.Application.SavedPlaces.Queries.GetSavedPlacesByUser;

public sealed class GetSavedPlacesByUserHandler
{
    private readonly ISavedPlaceRepository _savedPlaceRepository;

    public GetSavedPlacesByUserHandler(
        ISavedPlaceRepository savedPlaceRepository)
    {
        _savedPlaceRepository = savedPlaceRepository;
    }

    public async Task<IReadOnlyList<GetSavedPlacesByUserResult>> HandleAsync(
        GetSavedPlacesByUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var savedPlaces = await _savedPlaceRepository.GetByUserAsync(
            query.UserId,
            cancellationToken);

        if (savedPlaces.Count == 0)
        {
            return [];
        }

        var placeIds = savedPlaces
            .Select(x => x.PlaceId)
            .Distinct()
            .ToList();

        var places = await _savedPlaceRepository.GetPlacesByIdsAsync(
            placeIds,
            cancellationToken);

        var placeLookup = places.ToDictionary(p => p.Id);

        return savedPlaces
            .Select(saved => ToResult(saved, placeLookup))
            .ToList();
    }

    private static GetSavedPlacesByUserResult ToResult(
        SavedPlace saved,
        IReadOnlyDictionary<Guid, Place> places)
    {
        places.TryGetValue(saved.PlaceId, out var place);

        return new GetSavedPlacesByUserResult(
            saved.Id,
            saved.PlaceId,
            place?.Name,
            place?.Address,
            saved.Note,
            saved.CreatedAt);
    }
}
