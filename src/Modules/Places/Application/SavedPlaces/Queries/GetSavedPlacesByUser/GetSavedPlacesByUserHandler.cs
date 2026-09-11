using Places.Application.Abstractions;
using Places.Domain.Entities;

namespace Places.Application.SavedPlaces.Queries.GetSavedPlacesByUser;

public sealed class GetSavedPlacesByUserHandler
{
    private readonly ISavedPlaceRepository _savedPlaceRepository;
    private readonly IPlaceRepository _placeRepository;

    public GetSavedPlacesByUserHandler(
        ISavedPlaceRepository savedPlaceRepository,
        IPlaceRepository placeRepository
    )
    {
        _savedPlaceRepository = savedPlaceRepository;
        _placeRepository = placeRepository;
    }

    public async Task<IReadOnlyList<GetSavedPlacesByUserResult>> HandleAsync(
        GetSavedPlacesByUserQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var savedPlaces = await _savedPlaceRepository.GetByUserAsync(
            query.UserId,
            cancellationToken
        );

        if (savedPlaces.Count == 0)
        {
            return [];
        }

        var placeIds = savedPlaces
            .Select(x => x.PlaceId)
            .ToHashSet();

        var places = await Task.WhenAll(
            placeIds.Select(id =>
                _placeRepository.GetByIdAsync(id, cancellationToken))
        );

        var placeLookup = places
            .Where(p => p is not null)
            .Cast<Place>()
            .ToDictionary(p => p.Id);

        return savedPlaces
            .Select(saved => ToResult(saved, placeLookup))
            .ToList();
    }

    private static GetSavedPlacesByUserResult ToResult(
        SavedPlace saved,
        IReadOnlyDictionary<Guid, Place> places
    )
    {
        places.TryGetValue(saved.PlaceId, out var place);

        return new GetSavedPlacesByUserResult(
            saved.Id,
            saved.PlaceId,
            place?.Name,
            place?.Address,
            saved.Note,
            saved.CreatedAt
        );
    }
}
