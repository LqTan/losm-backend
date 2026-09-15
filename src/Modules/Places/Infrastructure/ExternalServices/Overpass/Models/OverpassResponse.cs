namespace Places.Infrastructure.ExternalServices.Overpass.Models;

internal sealed class OverpassResponse
{
    public List<OverpassElement> Elements { get; set; } = [];
}
