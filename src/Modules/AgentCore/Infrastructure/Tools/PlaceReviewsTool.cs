using System.Text.Json;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.PlaceReviews;
using Configuration.Application.Abstractions;
using Reviews.Application.Reviews.Queries.GetAverageRatingByPlace;
using Reviews.Application.Reviews.Queries.GetReviewsByPlace;

namespace AgentCore.Infrastructure.Tools;

public sealed class PlaceReviewsTool
    : AgentTool<PlaceReviewsToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly GetReviewsByPlaceHandler _getReviewsHandler;
    private readonly GetAverageRatingByPlaceHandler _getAverageRatingHandler;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;

    public PlaceReviewsTool(
        GetReviewsByPlaceHandler getReviewsHandler,
        GetAverageRatingByPlaceHandler getAverageRatingHandler,
        Configuration.Application.Abstractions.ITuningProvider tuning)
        : base(tuning, "get_place_reviews")
    {
        _getReviewsHandler = getReviewsHandler;
        _getAverageRatingHandler = getAverageRatingHandler;
        _tuning = tuning;
    }

    public override string Name => "get_place_reviews";

    public override string Description =>
        "Get community reviews and average rating for a place returned by place search.";

    protected override async Task<string> ExecuteAsync(
        PlaceReviewsToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        if (arguments.PlaceId == Guid.Empty)
        {
            throw new ArgumentException(
                "PlaceId is required.",
                nameof(arguments.PlaceId));
        }

        var opts = await _tuning.GetToolOptionsAsync(Name, cancellationToken);
        var limit = arguments.Limit > 0 ? arguments.Limit : opts.DefaultLimit;
        if (limit > opts.MaxLimit) limit = opts.MaxLimit;

        var reviews = await _getReviewsHandler.HandleAsync(
            new GetReviewsByPlaceQuery(arguments.PlaceId)
        );

        var averageRating = await _getAverageRatingHandler.HandleAsync(
            new GetAverageRatingByPlaceQuery(arguments.PlaceId)
        );

        var result = new
        {
            placeId = arguments.PlaceId,
            averageRating = averageRating.AverageRating,
            reviews = reviews
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new
                {
                    rating = x.Rating,
                    comment = x.Comment,
                    createdAt = x.CreatedAt
                })
                .ToList()
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
