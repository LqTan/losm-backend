using Overture.Application.Abstractions;
using Overture.Application.Contracts;

namespace Overture.Application.Settings.Queries.GetSettings;

public sealed class GetSettingsHandler
{
    private readonly ISettingsRepository _repo;

    public GetSettingsHandler(ISettingsRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<SettingResponse>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repo.GetAllAsync(cancellationToken);
        return all
            .Select(s => new SettingResponse(s.Key, s.ValueJson, s.UpdatedAt, s.UpdatedBy))
            .ToList();
    }

    public async Task<SettingResponse?> HandleByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var s = await _repo.GetByKeyAsync(key, cancellationToken);
        return s is null
            ? null
            : new SettingResponse(s.Key, s.ValueJson, s.UpdatedAt, s.UpdatedBy);
    }
}