namespace Places.Application.Abstractions;

public sealed record BoundingBox(
    double MinLatitude,
    double MinLongitude,
    double MaxLatitude,
    double MaxLongitude)
{
    public double CenterLatitude => (MinLatitude + MaxLatitude) / 2;
    public double CenterLongitude => (MinLongitude + MaxLongitude) / 2;

    public double MinLat => MinLatitude;
    public double MinLng => MinLongitude;
    public double MaxLat => MaxLatitude;
    public double MaxLng => MaxLongitude;
}