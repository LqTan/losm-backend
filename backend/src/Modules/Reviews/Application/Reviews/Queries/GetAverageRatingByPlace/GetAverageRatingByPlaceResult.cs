namespace Reviews.Application.Reviews.Queries.GetAverageRatingByPlace;

public sealed record GetAverageRatingByPlaceResult(
    Guid PlaceId,
    double? AverageRating
);