using Common.Application.Exceptions;
using Areas.Application.Abstractions;
using Areas.Application.Contracts;
using Areas.Domain.Entities;

namespace Areas.Application.Areas.Commands.CreateArea;

public sealed class CreateAreaHandler
{
    private readonly IAreaRepository _repo;

    public CreateAreaHandler(IAreaRepository repo)
    {
        _repo = repo;
    }

    public async Task<AreaResponse> HandleAsync(CreateAreaCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Name is required.");

        if (!Enum.IsDefined(typeof(AreaLevel), command.Level))
            throw new ValidationException($"Invalid level: {command.Level}");

        if (command.BboxMinLat >= command.BboxMaxLat || command.BboxMinLng >= command.BboxMaxLng)
            throw new ValidationException("Bbox is invalid (min must be less than max).");

        if (command.ParentId.HasValue)
        {
            var parent = await _repo.GetByIdAsync(command.ParentId.Value, cancellationToken);
            if (parent is null)
                throw new NotFoundException($"Parent area {command.ParentId} not found.");
            if ((int)command.Level <= (int)parent.Level)
                throw new ValidationException("Child level must be greater than parent level.");
        }

        var area = new Area(
            command.Name.Trim(),
            (AreaLevel)command.Level,
            command.BboxMinLat,
            command.BboxMinLng,
            command.BboxMaxLat,
            command.BboxMaxLng,
            command.ParentId,
            command.ExternalCode,
            command.IsActive);

        await _repo.AddAsync(area, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return AreaResponse.FromEntity(area);
    }
}