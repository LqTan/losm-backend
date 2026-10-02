namespace Places.Domain.Entities;

public class Place
{
    public Guid Id { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Address { get; private set; }

    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    public string? OpeningHours { get; private set; }
    public string? Category { get; private set; }
    public string Source { get; private set; } = null!;

    private Place() { }

    public Place(
        string externalId,
        string name,
        double latitude,
        double longitude,
        string source,
        string? address = null,
        string? category = null,
        string? openingHours = null)
    {
        Id = Guid.NewGuid();
        ExternalId = externalId;
        Name = name;
        Latitude = latitude;
        Longitude = longitude;
        Source = source;
        Address = address;
        Category = category;
        OpeningHours = openingHours;
    }

    public void UpdateDetails(
        string name,
        string? address,
        string? category,
        string? openingHours,
        double latitude,
        double longitude)
    {
        Name = name;
        Address = address;
        Category = category;
        OpeningHours = openingHours;
        Latitude = latitude;
        Longitude = longitude;
    }
}
