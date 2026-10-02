namespace Reviews.Domain.Entities;

public class Review
{
    public Guid Id { get; private set; }
    public Guid PlaceId { get; private set; }
    public Guid UserId { get; private set; }

    public int Rating { get; private set; }
    public string? Comment { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Review() {}

    public Review(
        Guid placeId,
        Guid userId,
        int rating,
        string? comment
    )
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                "Rating must be between 1 and 5."
            );
        Id = Guid.NewGuid();
        PlaceId = placeId;
        UserId = userId;
        Rating = rating;
        Comment = comment;
        CreatedAt = DateTime.UtcNow;
    }

    public void Update(int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                "Rating must be between 1 and 5."
            );
        Rating = rating;
        Comment = comment;
        UpdatedAt = DateTime.UtcNow;
    }
}