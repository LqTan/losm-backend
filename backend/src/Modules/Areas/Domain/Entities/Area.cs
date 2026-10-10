namespace Areas.Domain.Entities;

public enum AreaLevel
{
    Country = 0,
    Province = 1,
    District = 2,
    Commune = 3
}

public class Area
{
    public Guid Id { get; init; }
    public string Name { get; set; } = null!;
    public AreaLevel Level { get; set; }
    public Guid? ParentId { get; set; }
    public string? ExternalCode { get; set; }

    public double BboxMinLat { get; set; }
    public double BboxMinLng { get; set; }
    public double BboxMaxLat { get; set; }
    public double BboxMaxLng { get; set; }

    public bool IsActive { get; set; } = true;

    internal Area() { }

    public Area(
        string name,
        AreaLevel level,
        double bboxMinLat,
        double bboxMinLng,
        double bboxMaxLat,
        double bboxMaxLng,
        Guid? parentId = null,
        string? externalCode = null,
        bool isActive = true)
    {
        Id = Guid.NewGuid();
        Name = name;
        Level = level;
        ParentId = parentId;
        ExternalCode = externalCode;
        BboxMinLat = bboxMinLat;
        BboxMinLng = bboxMinLng;
        BboxMaxLat = bboxMaxLat;
        BboxMaxLng = bboxMaxLng;
        IsActive = isActive;
    }

    public void UpdateBbox(double minLat, double minLng, double maxLat, double maxLng)
    {
        BboxMinLat = minLat;
        BboxMinLng = minLng;
        BboxMaxLat = maxLat;
        BboxMaxLng = maxLng;
    }

    public void Rename(string name)
    {
        Name = name;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
    }

    public void SetParent(Guid? parentId)
    {
        ParentId = parentId;
    }
}