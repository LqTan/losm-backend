using Configuration.Application.Models;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Infrastructure.Elasticsearch;

public sealed class EsConfigurationStore : Application.Abstractions.IConfigurationStore
{
    private readonly HttpClient _httpClient;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<EsConfigurationStore> _logger;

    public EsConfigurationStore(
        HttpClient httpClient,
        IOptions<ElasticsearchOptions> options,
        ILogger<EsConfigurationStore> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ConfigurationDoc?> GetAsync(string scope, CancellationToken ct)
    {
        var url = $"{_options.Url.TrimEnd('/')}/{_options.ConfigIndex}/_doc/{Uri.EscapeDataString(scope)}";
        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<EsGetResponse>(cancellationToken: ct);
            if (json is null || !json.Found || json.Source is null) return null;

            return new ConfigurationDoc(
                json.Source.Scope,
                json.Source.Module,
                json.Source.Key,
                json.Source.ValueJson,
                json.Source.Version,
                json.Source.UpdatedAt,
                json.Source.UpdatedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ES get failed for scope {Scope}", scope);
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> ListScopesAsync(CancellationToken ct)
    {
        var url = $"{_options.Url.TrimEnd('/')}/{_options.ConfigIndex}/_search?size=10000";
        var body = new { _source = new[] { "scope" }, query = new { match_all = new { } } };
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<EsSearchResponse>(cancellationToken: ct);
            return json?.Hits?.Hits?
                .Select(h => h.Source?.Scope)
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ES list scopes failed");
            return Array.Empty<string>();
        }
    }

    public async Task<UpsertResult> UpsertAsync(
        string scope,
        string module,
        string key,
        string valueJson,
        long? expectedVersion,
        Guid? updatedBy,
        CancellationToken ct)
    {
        try
        {
            long currentVersion = 0;
            var current = await GetAsync(scope, ct);
            if (current is not null) currentVersion = current.Version;

            if (expectedVersion.HasValue && currentVersion != expectedVersion.Value)
            {
                return new UpsertResult(false, currentVersion,
                    $"ETag mismatch: expected {expectedVersion}, current {currentVersion}");
            }

            var newVersion = currentVersion + 1;
            var updatedAt = DateTime.UtcNow;

            var doc = new ConfigurationEntryPayload
            {
                Scope = scope,
                Module = module,
                Key = key,
                ValueJson = valueJson,
                Version = newVersion,
                UpdatedAt = updatedAt,
                UpdatedBy = updatedBy
            };

            var url = $"{_options.Url.TrimEnd('/')}/{_options.ConfigIndex}/_doc/{Uri.EscapeDataString(scope)}?refresh=true";
            var response = await _httpClient.PutAsJsonAsync(url, doc, ct);
            response.EnsureSuccessStatusCode();

            await WriteHistoryAsync(scope, current?.ValueJson, valueJson,
                currentVersion, newVersion, updatedAt, updatedBy, "updated", ct);

            return new UpsertResult(true, newVersion, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ES upsert failed for {Scope}", scope);
            return new UpsertResult(false, 0, ex.Message);
        }
    }

    public async Task<DeleteResult> DeleteAsync(
        string scope,
        long expectedVersion,
        Guid? deletedBy,
        CancellationToken ct)
    {
        try
        {
            var current = await GetAsync(scope, ct);
            if (current is null)
            {
                return new DeleteResult(false, "Not found");
            }

            if (current.Version != expectedVersion)
            {
                return new DeleteResult(false,
                    $"ETag mismatch: expected {expectedVersion}, current {current.Version}");
            }

            var url = $"{_options.Url.TrimEnd('/')}/{_options.ConfigIndex}/_doc/{Uri.EscapeDataString(scope)}?refresh=true";
            var response = await _httpClient.DeleteAsync(url, ct);
            response.EnsureSuccessStatusCode();

            await WriteHistoryAsync(scope, current.ValueJson, "{}",
                current.Version, current.Version, DateTime.UtcNow, deletedBy, "deleted", ct);

            return new DeleteResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ES delete failed for {Scope}", scope);
            return new DeleteResult(false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<ConfigurationChange>> GetHistoryAsync(
        string scope, int limit, CancellationToken ct)
    {
        var url = $"{_options.Url.TrimEnd('/')}/{_options.HistoryIndex}/_search";
        var body = new
        {
            size = limit,
            sort = new object[]
            {
                new { changedAt = new { order = "desc" } }
            },
            query = new
            {
                term = new { scope = new { value = scope } }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<EsHistorySearchResponse>(cancellationToken: ct);
            return json?.Hits?.Hits?
                .Select(h => new ConfigurationChange(
                    h.Source.Scope,
                    h.Source.OldValueJson,
                    h.Source.NewValueJson,
                    h.Source.OldVersion,
                    h.Source.NewVersion,
                    h.Source.ChangedAt,
                    h.Source.ChangedBy,
                    h.Source.Action))
                .ToList() ?? new List<ConfigurationChange>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ES history failed for {Scope}", scope);
            return Array.Empty<ConfigurationChange>();
        }
    }

    public async Task<long> GetCurrentVersionAsync(CancellationToken ct)
    {
        var url = $"{_options.Url.TrimEnd('/')}/{_options.ConfigIndex}/_search";
        var body = new
        {
            size = 0,
            aggs = new
            {
                max_version = new { max = new { field = "version" } }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, body, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("aggregations", out var aggs)) return 0;
            if (!aggs.TryGetProperty("max_version", out var max)) return 0;
            if (!max.TryGetProperty("value", out var val)) return 0;
            if (val.ValueKind == JsonValueKind.Null) return 0;
            return val.GetDouble() > 0 ? (long)val.GetDouble() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ES version check failed");
            return 0;
        }
    }

    private async Task WriteHistoryAsync(
        string scope,
        string? oldValueJson,
        string newValueJson,
        long oldVersion,
        long newVersion,
        DateTime changedAt,
        Guid? changedBy,
        string action,
        CancellationToken ct)
    {
        var entry = new ConfigurationHistoryPayload
        {
            Scope = scope,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            OldVersion = oldVersion,
            NewVersion = newVersion,
            ChangedAt = changedAt,
            ChangedBy = changedBy,
            Action = action
        };

        var url = $"{_options.Url.TrimEnd('/')}/{_options.HistoryIndex}/_doc?refresh=true";
        await _httpClient.PostAsJsonAsync(url, entry, ct);
    }

    private sealed class EsGetResponse
    {
        public bool Found { get; set; }
        public ConfigurationEntryPayload? Source { get; set; }
    }

    private sealed class EsSearchResponse
    {
        public EsHits? Hits { get; set; }
    }

    private sealed class EsHistorySearchResponse
    {
        public EsHistoryHits? Hits { get; set; }
    }

    private sealed class EsHits
    {
        public List<EsHitItem>? Hits { get; set; }
    }

    private sealed class EsHistoryHits
    {
        public List<EsHistoryHitItem>? Hits { get; set; }
    }

    private sealed class EsHitItem
    {
        public ConfigurationEntryPayload? Source { get; set; }
    }

    private sealed class EsHistoryHitItem
    {
        public ConfigurationHistoryPayload Source { get; set; } = null!;
    }

    private sealed class ConfigurationEntryPayload
    {
        public string Scope { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string ValueJson { get; set; } = "{}";
        public long Version { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    private sealed class ConfigurationHistoryPayload
    {
        public string Scope { get; set; } = string.Empty;
        public string? OldValueJson { get; set; }
        public string NewValueJson { get; set; } = "{}";
        public long OldVersion { get; set; }
        public long NewVersion { get; set; }
        public DateTime ChangedAt { get; set; }
        public Guid? ChangedBy { get; set; }
        public string Action { get; set; } = "updated";
    }
}
