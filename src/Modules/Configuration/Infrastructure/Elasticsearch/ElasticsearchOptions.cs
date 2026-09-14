namespace Configuration.Infrastructure.Elasticsearch;

public sealed class ElasticsearchOptions
{
    public string Url { get; set; } = "http://localhost:9200";
    public string ConfigIndex { get; set; } = "losm-config-entries";
    public string HistoryIndex { get; set; } = "losm-config-history";
    public int RequestTimeoutSeconds { get; set; } = 5;
    public string? ServiceToken { get; set; }
}
