using Areas.Application.Abstractions;
using Areas.Domain.Entities;
using Areas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Areas.Infrastructure.Repositories;

public sealed class AreaRepository : IAreaRepository
{
    private readonly AreasDbContext _db;

    public AreaRepository(AreasDbContext db)
    {
        _db = db;
    }

    public Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Areas.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Area>> GetAllAsync(CancellationToken ct = default)
        => await _db.Areas
            .AsNoTracking()
            .OrderBy(x => x.Level).ThenBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Area>> GetByLevelAsync(AreaLevel level, CancellationToken ct = default)
        => await _db.Areas
            .AsNoTracking()
            .Where(x => x.Level == level)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Area>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
        => await _db.Areas
            .AsNoTracking()
            .Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Area area, CancellationToken ct = default)
        => await _db.Areas.AddAsync(area, ct);

    public void Update(Area area)
        => _db.Areas.Update(area);

    public void Remove(Area area)
        => _db.Areas.Remove(area);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}