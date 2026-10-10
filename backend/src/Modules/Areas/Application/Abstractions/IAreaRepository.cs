using Areas.Domain.Entities;

namespace Areas.Application.Abstractions;

public interface IAreaRepository
{
    Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Area>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Area>> GetByLevelAsync(AreaLevel level, CancellationToken ct = default);
    Task<IReadOnlyList<Area>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task AddAsync(Area area, CancellationToken ct = default);
    void Update(Area area);
    void Remove(Area area);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}