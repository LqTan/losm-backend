using Configuration.Application.Abstractions;

namespace Configuration.Application.Admin.Commands.UpsertConfiguration;

public sealed class UpsertConfigurationHandler
{
    private readonly IConfigurationStore _store;

    public UpsertConfigurationHandler(IConfigurationStore store)
    {
        _store = store;
    }

    public async Task<UpsertConfigurationResult> HandleAsync(
        UpsertConfigurationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Scope))
        {
            return new UpsertConfigurationResult(false, 0, "Scope is required");
        }

        var result = await _store.UpsertAsync(
            command.Scope,
            command.Module,
            command.Key,
            command.ValueJson,
            command.ExpectedVersion,
            command.UpdatedBy,
            cancellationToken);

        return UpsertConfigurationResult.From(result);
    }
}
