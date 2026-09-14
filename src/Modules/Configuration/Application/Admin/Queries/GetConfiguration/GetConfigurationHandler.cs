using Configuration.Application.Abstractions;

namespace Configuration.Application.Admin.Queries.GetConfiguration;

public sealed class GetConfigurationHandler
{
    private readonly IConfigurationStore _store;

    public GetConfigurationHandler(IConfigurationStore store)
    {
        _store = store;
    }

    public async Task<GetConfigurationResult?> HandleAsync(
        GetConfigurationQuery query,
        CancellationToken cancellationToken = default)
    {
        var doc = await _store.GetAsync(query.Scope, cancellationToken);
        if (doc is null) return null;

        return new GetConfigurationResult(
            doc.Scope,
            doc.Module,
            doc.Key,
            doc.ValueJson,
            doc.Version,
            doc.UpdatedAt,
            doc.UpdatedBy
        );
    }
}
