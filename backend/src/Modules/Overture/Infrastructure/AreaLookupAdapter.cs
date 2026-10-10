using Areas.Application.Abstractions;
using Overture.Application.Jobs;

namespace Overture.Infrastructure;

public sealed class AreaLookupAdapter : IAreaLookup
{
    private readonly IAreaRepository _repo;

    public AreaLookupAdapter(IAreaRepository repo)
    {
        _repo = repo;
    }

    public async Task<AreaSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var area = await _repo.GetByIdAsync(id, ct);
        return area is null
            ? null
            : new AreaSnapshot(
                area.Id,
                area.Name,
                (int)area.Level,
                area.BboxMinLat,
                area.BboxMinLng,
                area.BboxMaxLat,
                area.BboxMaxLng);
    }
}