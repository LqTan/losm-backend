using Places.Application.Abstractions;
using Search.Application.Abstractions;

namespace Search.Application.Search.Queries.SearchPlaces;

public sealed class SearchPlacesHandler
{
    private readonly IPlaceSearchService _placeSearchService;
    private readonly IRelevanceScorer _relevanceScorer;
    private readonly IRankingService _rankingService;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;

    public SearchPlacesHandler(
        IPlaceSearchService placeSearchService,
        IRelevanceScorer relevanceScorer,
        IRankingService rankingService,
        Configuration.Application.Abstractions.ITuningProvider tuning)
    {
        _placeSearchService = placeSearchService;
        _relevanceScorer = relevanceScorer;
        _rankingService = rankingService;
        _tuning = tuning;
    }

    public async Task<IReadOnlyList<SearchResult>> HandleAsync(
        SearchPlacesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Query))
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        if (query.Box is null &&
            (query.Latitude is null || query.Longitude is null))
        {
            throw new ArgumentException(
                "Either (Latitude, Longitude) or Box must be provided.",
                nameof(query));
        }

        var searchOptions = await _tuning.GetSearchOptionsAsync(cancellationToken);

        var radiusKm = query.RadiusKm.HasValue && query.RadiusKm.Value > 0
            ? query.RadiusKm.Value
            : searchOptions.DefaultRadiusKm;

        var candidateLimit = query.CandidateLimit.HasValue && query.CandidateLimit.Value > 0
            ? query.CandidateLimit.Value
            : searchOptions.CandidateLimit;

        IReadOnlyList<PlaceCandidate> candidates;
        double referenceLat;
        double referenceLon;
        BoundingBox? searchBox = query.Box;

        if (searchBox is not null)
        {
            candidates = await _placeSearchService.SearchByBoundingBoxAsync(
                query.Query,
                searchBox,
                candidateLimit,
                query.Amenity,
                cancellationToken
            );
            referenceLat = searchBox.CenterLatitude;
            referenceLon = searchBox.CenterLongitude;
        }
        else
        {
            candidates = await _placeSearchService.SearchAsync(
                query.Query,
                query.Latitude!.Value,
                query.Longitude!.Value,
                radiusKm,
                candidateLimit,
                cancellationToken: cancellationToken
            );
            referenceLat = query.Latitude.Value;
            referenceLon = query.Longitude.Value;
        }

        var relevanceScores = await _relevanceScorer.ScoreAsync(
            query.Query,
            candidates,
            cancellationToken
        );

        var scoreByPlaceId = relevanceScores.ToDictionary(
            x => x.PlaceId,
            x => x.Score
        );

        var qNorm = query.Query.Trim().ToLowerInvariant();
        var qTokens = qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var results = new List<SearchResult>(candidates.Count);

        foreach (var place in candidates)
        {
            var relevanceScore = scoreByPlaceId.GetValueOrDefault(place.Id);
            var distanceKm = _rankingService.CalculateDistanceKm(
                referenceLat,
                referenceLon,
                place.Latitude,
                place.Longitude
            );

            // Google Maps style ranking:
            // 1. Độ khớp tên địa điểm (Exact / Prefix / Full token match)
            // 2. Khoảng cách gần nhất từ vị trí người dùng (DistanceKm tăng dần)
            var pNameLower = place.Name.ToLowerInvariant();
            int nameTier = 0;
            if (pNameLower.Equals(qNorm, StringComparison.OrdinalIgnoreCase))
            {
                nameTier = 3; // Trùng khớp hoàn toàn tên
            }
            else if (pNameLower.StartsWith(qNorm, StringComparison.OrdinalIgnoreCase))
            {
                nameTier = 2; // Bắt đầu bằng từ khóa
            }
            else if (qTokens.Length > 0 && qTokens.All(t => pNameLower.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                nameTier = 1; // Chứa tất cả các từ trong query
            }

            // Điểm số theo chuẩn Google Maps:
            // Tầng tên cao hơn sẽ luôn đứng trước; trong cùng tầng tên, quán gần hơn sẽ có điểm cao hơn
            var finalScore = (nameTier * 100.0) + (10.0 / (1.0 + distanceKm));

            results.Add(new SearchResult(
                place.Id,
                place.Name,
                place.Address,
                place.Latitude,
                place.Longitude,
                place.Category,
                place.OpeningHours,
                relevanceScore,
                Math.Round(distanceKm, 2),
                Math.Round(finalScore, 4)
            ));
        }

        var topK = query.CandidateLimit ?? searchOptions.TopK;
        return results
            .OrderByDescending(x => x.FinalScore) // Tương đương: nameTier giảm dần, distanceKm tăng dần
            .Take(topK > 0 ? topK : results.Count)
            .ToList();
    }
}
