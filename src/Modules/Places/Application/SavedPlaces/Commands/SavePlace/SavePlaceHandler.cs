using Places.Application.Abstractions;
using Places.Domain.Entities;

namespace Places.Application.SavedPlaces.Commands.SavePlace;

public sealed class SavePlaceHandler
{
    private readonly ISavedPlaceRepository _savedPlaceRepository;
    private readonly IPlaceRepository _placeRepository;

    public SavePlaceHandler(
        ISavedPlaceRepository savedPlaceRepository,
        IPlaceRepository placeRepository
    )
    {
        _savedPlaceRepository = savedPlaceRepository;
        _placeRepository = placeRepository;
    }

    public async Task<SavePlaceResult> HandleAsync(
        SavePlaceCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var place = await _placeRepository.GetByIdAsync(
            command.PlaceId,
            cancellationToken
        );

        if (place is null)
        {
            throw new KeyNotFoundException(
                $"Place '{command.PlaceId}' was not found."
            );
        }

        var existing = await _savedPlaceRepository.GetAsync(
            command.UserId,
            command.PlaceId,
            cancellationToken
        );

        if (existing is not null)
        {
            existing.UpdateNote(command.Note);
            await _savedPlaceRepository.AddAsync(
                existing,
                cancellationToken
            );
            return new SavePlaceResult(
                existing.Id,
                existing.UserId,
                existing.PlaceId,
                existing.Note,
                existing.CreatedAt
            );
        }

        var savedPlace = new SavedPlace(
            command.UserId,
            command.PlaceId,
            command.Note
        );

        await _savedPlaceRepository.AddAsync(
            savedPlace,
            cancellationToken
        );

        return new SavePlaceResult(
            savedPlace.Id,
            savedPlace.UserId,
            savedPlace.PlaceId,
            savedPlace.Note,
            savedPlace.CreatedAt
        );
    }
}
