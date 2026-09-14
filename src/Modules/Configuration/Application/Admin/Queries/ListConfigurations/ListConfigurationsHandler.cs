using Configuration.Application.Abstractions;

namespace Configuration.Application.Admin.Queries.ListConfigurations;

public sealed class ListConfigurationsHandler
{
    private readonly IConfigurationStore _store;

    public ListConfigurationsHandler(IConfigurationStore store)
    {
        _store = store;
    }

    public async Task<ListConfigurationsResult> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var scopes = await _store.ListScopesAsync(cancellationToken);
        var version = await _store.GetCurrentVersionAsync(cancellationToken);
        return new ListConfigurationsResult(scopes, version);
    }
}
