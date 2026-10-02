using System.Net.Http.Json;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class HttpRElevanceScorer : IRelevanceScorer
{
    private readonly HttpClient _httpClient;
    public HttpRElevanceScorer(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    public async Task<IReadOnlyList<RelevanceScore>> ScoreAsync(
        string query,
        IReadOnlyList<PlaceCandidate> candidates,
        CancellationToken cancellationToken
    )
    {
        var request = new
        {
            query,
            candidates = candidates.Select(x => new
            {
                id = x.Id,
                name = x.Name,
                address = x.Address,
                category = x.Category
            })
        };
        var response = await _httpClient.PostAsJsonAsync(
            "api/rerank",
            request,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        var result = await response.Content
            .ReadFromJsonAsync<RerankResponse>(
                cancellationToken: cancellationToken
            );
        return result?.Results
            .Select(x => new RelevanceScore(x.Id, x.RelevanceScore))
            .ToList()
            ?? [];
    }

    private sealed record RerankResponse(
        IReadOnlyList<RerankResult> Results
    );
    private sealed record RerankResult(
        Guid Id,
        double RelevanceScore
    );
}