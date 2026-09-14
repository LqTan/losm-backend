using Configuration.Application.Abstractions;

namespace Configuration.Application.Admin.Commands.DeleteConfiguration;

public sealed class DeleteConfigurationHandler
{
    private readonly IConfigurationStore _store;

    public DeleteConfigurationHandler(IConfigurationStore store)
    {
        _store = store;
    }

    public async Task<DeleteConfigurationResult> HandleAsync(
        DeleteConfigurationCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _store.DeleteAsync(
            command.Scope,
            command.ExpectedVersion,
            command.DeletedBy,
            cancellationToken);

        return new DeleteConfigurationResult(result.Succeeded, result.Error);
    }
}
