namespace Places.Infrastructure.ExternalServices.Overpass.Models;

internal sealed class OverpassElement
{
    public string Type { get; set; } = string.Empty;
    public long Id { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
