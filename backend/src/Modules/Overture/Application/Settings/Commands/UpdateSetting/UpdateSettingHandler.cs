using Common.Application.Exceptions;
using Overture.Application.Abstractions;
using Overture.Application.Contracts;
using Overture.Domain.Entities;

namespace Overture.Application.Settings.Commands.UpdateSetting;

public sealed class UpdateSettingHandler
{
    private readonly ISettingsRepository _repo;

    public UpdateSettingHandler(ISettingsRepository repo)
    {
        _repo = repo;
    }

    public async Task<SettingResponse> HandleAsync(
        UpdateSettingCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Key))
            throw new ValidationException("Key is required.");

        var existing = await _repo.GetByKeyAsync(command.Key, cancellationToken);
        if (existing is null)
        {
            existing = new Setting(
                command.Key.Trim(),
                command.ValueJson ?? "{}",
                command.UpdatedBy);
            await _repo.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Update(command.ValueJson ?? "{}", command.UpdatedBy);
            _repo.Update(existing);
        }
        await _repo.SaveChangesAsync(cancellationToken);
        return new SettingResponse(
            existing.Key,
            existing.ValueJson,
            existing.UpdatedAt,
            existing.UpdatedBy);
    }
}