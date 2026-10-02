using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Queries.GetAverageRatingByPlace;
using Reviews.Application.Reviews.Queries.GetReviewsByPlace;
using Reviews.Application.Reviews.Queries.GetReviewsByUser;

namespace Reviews.Presentation;

[ApiController]
[Route("api/reviews")]
[Authorize]
public sealed class ReviewsController : ControllerBase
{
    private readonly CreateReviewHandler _createReviewHandler;
    private readonly GetReviewsByPlaceHandler _getReviewsByPlaceHandler;
    private readonly GetAverageRatingByPlaceHandler _getAverageRatingByPlaceHandler;
    private readonly GetReviewsByUserHandler _getReviewsByUserHandler;

    public ReviewsController(
        CreateReviewHandler createReviewHandler,
        GetReviewsByPlaceHandler getReviewsByPlaceHandler,
        GetAverageRatingByPlaceHandler getAverageRatingByPlaceHandler,
        GetReviewsByUserHandler getReviewsByUserHandler)
    {
        _createReviewHandler = createReviewHandler;
        _getReviewsByPlaceHandler = getReviewsByPlaceHandler;
        _getAverageRatingByPlaceHandler = getAverageRatingByPlaceHandler;
        _getReviewsByUserHandler = getReviewsByUserHandler;
    }

    [HttpPost]
    public async Task<ActionResult<CreateReviewResult>> Create(
        [FromBody] CreateReviewApiRequest body,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _createReviewHandler.HandleAsync(
            new CreateReviewCommand(
                body.PlaceId,
                userId,
                body.Rating,
                body.Comment),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<GetReviewsByUserResult>>> GetMine(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var effectiveLimit = limit is > 0 and <= 200 ? limit.Value : 100;
        var query = new GetReviewsByUserQuery(userId, effectiveLimit);
        var result = await _getReviewsByUserHandler.HandleAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("place/{placeId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<GetReviewsByPlaceResult>>> GetByPlace(
        Guid placeId,
        CancellationToken cancellationToken)
    {
        var query = new GetReviewsByPlaceQuery(placeId);
        var result = await _getReviewsByPlaceHandler.HandleAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("place/{placeId:guid}/average-rating")]
    [AllowAnonymous]
    public async Task<ActionResult<GetAverageRatingByPlaceResult>> GetAverageRating(
        Guid placeId,
        CancellationToken cancellationToken)
    {
        var query = new GetAverageRatingByPlaceQuery(placeId);
        var result = await _getAverageRatingByPlaceHandler.HandleAsync(query, cancellationToken);
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }
}

public sealed record CreateReviewApiRequest(
    Guid PlaceId,
    int Rating,
    string? Comment
);
