using Microsoft.EntityFrameworkCore;
using Overture.Application.Abstractions;
using Overture.Domain.Entities;
using Overture.Infrastructure.Persistence;

namespace Overture.Infrastructure.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly OvertureDbContext _db;

    public SettingsRepository(OvertureDbContext db)
    {
        _db = db;
    }

    public Task<Setting?> GetByKeyAsync(string key, CancellationToken ct = default)
        => _db.Settings.FirstOrDefaultAsync(x => x.Key == key, ct);

    public async Task<IReadOnlyList<Setting>> GetAllAsync(CancellationToken ct = default)
        => await _db.Settings.AsNoTracking().OrderBy(x => x.Key).ToListAsync(ct);

    public async Task AddAsync(Setting setting, CancellationToken ct = default)
        => await _db.Settings.AddAsync(setting, ct);

    public void Update(Setting setting)
        => _db.Settings.Update(setting);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}