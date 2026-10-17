namespace Places.Domain.Entities;

public sealed class CategoryMapping
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
