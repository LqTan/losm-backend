namespace Places.Infrastructure.ExternalServices.Here.Models;

internal sealed class HereItem
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public HerePosition? Position { get; set; }
    public HereAddress? Address { get; set; }
    public List<HereCategory>? Categories { get; set; }
    public List<HereOpeningHours>? OpeningHours { get; set; }
}
