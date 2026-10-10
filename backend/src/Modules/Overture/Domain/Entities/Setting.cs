namespace Overture.Domain.Entities;

public class Setting
{
    public Guid Id { get; init; }
    public string Key { get; set; } = null!;
    public string ValueJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    internal Setting() { }

    public Setting(string key, string valueJson, Guid? updatedBy = null)
    {
        Id = Guid.NewGuid();
        Key = key;
        ValueJson = valueJson;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string valueJson, Guid? updatedBy = null)
    {
        ValueJson = valueJson;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}