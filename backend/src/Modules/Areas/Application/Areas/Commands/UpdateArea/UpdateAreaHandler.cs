using Common.Application.Exceptions;
using Areas.Application.Abstractions;
using Areas.Application.Contracts;

namespace Areas.Application.Areas.Commands.UpdateArea;

public sealed class UpdateAreaHandler
{
    private readonly IAreaRepository _repo;

    public UpdateAreaHandler(IAreaRepository repo)
    {
        _repo = repo;
    }

    public async Task<AreaResponse> HandleAsync(UpdateAreaCommand command, CancellationToken cancellationToken = default)
    {
        var area = await _repo.GetByIdAsync(command.Id, cancellationToken);
        if (area is null)
            throw new NotFoundException($"Area {command.Id} not found.");

        if (command.BboxMinLat >= command.BboxMaxLat || command.BboxMinLng >= command.BboxMaxLng)
            throw new ValidationException("Bbox is invalid.");

        if (command.ParentId.HasValue)
        {
            if (command.ParentId == area.Id)
                throw new ValidationException("An area cannot be its own parent.");

            var parent = await _repo.GetByIdAsync(command.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new NotFoundException($"Parent area {command.ParentId} not found.");
        }

        area.Rename(command.Name);
        area.SetParent(command.ParentId);
        area.UpdateBbox(
            command.BboxMinLat,
            command.BboxMinLng,
            command.BboxMaxLat,
            command.BboxMaxLng);
        area.SetActive(command.IsActive);

        _repo.Update(area);
        await _repo.SaveChangesAsync(cancellationToken);

        return AreaResponse.FromEntity(area);
    }
}