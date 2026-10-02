using Places.Application.Abstractions;

namespace Places.Application.SavedPlaces.Commands.UnsavePlace;

public sealed class UnsavePlaceHandler
{
    private readonly ISavedPlaceRepository _savedPlaceRepository;

    public UnsavePlaceHandler(ISavedPlaceRepository savedPlaceRepository)
    {
        _savedPlaceRepository = savedPlaceRepository;
    }

    public async Task<UnsavePlaceResult> HandleAsync(
        UnsavePlaceCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await _savedPlaceRepository.GetAsync(
            command.UserId,
            command.PlaceId,
            cancellationToken
        );

        if (existing is null)
        {
            return new UnsavePlaceResult(
                command.UserId,
                command.PlaceId,
                false
            );
        }

        await _savedPlaceRepository.RemoveAsync(
            existing,
            cancellationToken
        );

        return new UnsavePlaceResult(
            command.UserId,
            command.PlaceId,
            true
        );
    }
}
