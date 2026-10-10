using Overture.Domain.Entities;

namespace Overture.Application.Abstractions;

public interface ISettingsRepository
{
    Task<Setting?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Setting setting, CancellationToken ct = default);
    void Update(Setting setting);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}