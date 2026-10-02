namespace Places.Domain.Entities;

public class SavedPlace
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PlaceId { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SavedPlace(){}

    public SavedPlace(
        Guid userId,
        Guid placeId,
        string? note = null
    )
    {
        if (userId == Guid.Empty)
            throw new ArgumentException(
                "UserId is required.",
                nameof(userId)
            );
        if (placeId == Guid.Empty)
            throw new ArgumentException(
                "PlaceId is required.",
                nameof(placeId)
            );

        Id = Guid.NewGuid();
        UserId = userId;
        PlaceId = placeId;
        Note = note;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateNote(string? note)
    {
        Note = note;
    }
}
