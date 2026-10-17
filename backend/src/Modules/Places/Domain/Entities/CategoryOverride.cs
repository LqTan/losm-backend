namespace Places.Domain.Entities;

public sealed class CategoryOverride
{
    public int Id { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string TargetCategory { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
