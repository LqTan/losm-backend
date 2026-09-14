using Configuration.Application.Models;

namespace Configuration.Application.Abstractions;

public interface IConfigurationStore
{
    Task<ConfigurationDoc?> GetAsync(string scope, CancellationToken ct);
    Task<IReadOnlyList<string>> ListScopesAsync(CancellationToken ct);
    Task<UpsertResult> UpsertAsync(
        string scope,
        string module,
        string key,
        string valueJson,
        long? expectedVersion,
        Guid? updatedBy,
        CancellationToken ct
    );
    Task<DeleteResult> DeleteAsync(
        string scope,
        long expectedVersion,
        Guid? deletedBy,
        CancellationToken ct
    );
    Task<IReadOnlyList<ConfigurationChange>> GetHistoryAsync(
        string scope,
        int limit,
        CancellationToken ct
    );
    Task<long> GetCurrentVersionAsync(CancellationToken ct);
}
