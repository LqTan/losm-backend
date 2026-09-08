using System.Text.Json;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.PlaceReviews;
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

    public PlaceReviewsTool(
        GetReviewsByPlaceHandler getReviewsHandler,
        GetAverageRatingByPlaceHandler getAverageRatingHandler
    )
    {
        _getReviewsHandler = getReviewsHandler;
        _getAverageRatingHandler = getAverageRatingHandler;
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
                nameof(arguments.PlaceId)
            );
        }

        if (arguments.Limit is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arguments.Limit)
            );
        }

        var reviews = await _getReviewsHandler.HandleAsync(
            new GetReviewsByPlaceQuery(
                arguments.PlaceId
            )
        );

        var averageRating =
            await _getAverageRatingHandler.HandleAsync(
                new GetAverageRatingByPlaceQuery(
                    arguments.PlaceId
                )
            );

        var result = new
        {
            placeId = arguments.PlaceId,
            averageRating = averageRating.AverageRating,

            reviews = reviews
                .OrderByDescending(x => x.CreatedAt)
                .Take(arguments.Limit)
                .Select(x => new
                {
                    rating = x.Rating,
                    comment = x.Comment,
                    createdAt = x.CreatedAt
                })
                .ToList()
        };

        return JsonSerializer.Serialize(
            result,
            JsonOptions
        );
    }
}
