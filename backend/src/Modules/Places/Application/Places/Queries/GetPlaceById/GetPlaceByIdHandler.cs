using Places.Application.Abstractions;
using Places.Domain.Entities;

namespace Places.Application.Places.Queries.GetPlaceById;

public sealed class GetPlaceByIdHandler
{
    private readonly IPlaceRepository _placeRepository;
    public GetPlaceByIdHandler(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }
    public async Task<Place?> HandleAsync(
        GetPlaceByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await _placeRepository.GetByIdAsync(
            query.Id,
            cancellationToken
        );
    }
}