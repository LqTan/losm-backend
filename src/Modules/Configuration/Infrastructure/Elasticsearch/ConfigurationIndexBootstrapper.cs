using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Configuration.Infrastructure.Elasticsearch;

public sealed class ConfigurationIndexBootstrapper
{
    private readonly HttpClient _httpClient;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ConfigurationIndexBootstrapper> _logger;

    public ConfigurationIndexBootstrapper(
        HttpClient httpClient,
        IOptions<ElasticsearchOptions> options,
        ILogger<ConfigurationIndexBootstrapper> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureIndicesAsync(CancellationToken ct)
    {
        await EnsureIndexAsync(_options.ConfigIndex, IndexMappings.ConfigIndexMapping, ct);
        await EnsureIndexAsync(_options.HistoryIndex, IndexMappings.HistoryIndexMapping, ct);
    }

    private async Task EnsureIndexAsync(string indexName, object mapping, CancellationToken ct)
    {
        var baseUrl = _options.Url.TrimEnd('/');
        try
        {
            var headResponse = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, $"{baseUrl}/{indexName}"), ct);

            if (headResponse.IsSuccessStatusCode) return;

            var response = await _httpClient.PutAsJsonAsync(
                $"{baseUrl}/{indexName}", mapping, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Failed to create ES index {Index}: {Status} {Body}",
                    indexName, response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Created ES index {Index}", indexName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index bootstrap failed for {Index}", indexName);
        }
    }
}

internal static class IndexMappings
{
    public static readonly object ConfigIndexMapping = new
    {
        mappings = new
        {
            properties = new
            {
                scope = new { type = "keyword" },
                module = new { type = "keyword" },
                key = new { type = "keyword" },
                valueJson = new { type = "text", index = false },
                version = new { type = "long" },
                updatedAt = new { type = "date" },
                updatedBy = new { type = "keyword" }
            }
        }
    };

    public static readonly object HistoryIndexMapping = new
    {
        mappings = new
        {
            properties = new
            {
                scope = new { type = "keyword" },
                oldValueJson = new { type = "text", index = false },
                newValueJson = new { type = "text", index = false },
                oldVersion = new { type = "long" },
                newVersion = new { type = "long" },
                changedAt = new { type = "date" },
                changedBy = new { type = "keyword" },
                action = new { type = "keyword" }
            }
        }
    };
}
