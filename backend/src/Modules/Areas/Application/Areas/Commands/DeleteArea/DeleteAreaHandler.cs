using Common.Application.Exceptions;
using Areas.Application.Abstractions;

namespace Areas.Application.Areas.Commands.DeleteArea;

public sealed class DeleteAreaHandler
{
    private readonly IAreaRepository _repo;

    public DeleteAreaHandler(IAreaRepository repo)
    {
        _repo = repo;
    }

    public async Task HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var area = await _repo.GetByIdAsync(id, cancellationToken);
        if (area is null)
            throw new NotFoundException($"Area {id} not found.");

        var children = await _repo.GetChildrenAsync(id, cancellationToken);
        if (children.Count > 0)
            throw new ValidationException("Cannot delete an area that has children.");

        _repo.Remove(area);
        await _repo.SaveChangesAsync(cancellationToken);
    }
}