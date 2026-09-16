namespace Places.Application.Abstractions;

public sealed record BoundingBox(
    double MinLatitude,
    double MaxLatitude,
    double MinLongitude,
    double MaxLongitude
)
{
    public double CenterLatitude => (MinLatitude + MaxLatitude) / 2.0;
    public double CenterLongitude => (MinLongitude + MaxLongitude) / 2.0;

    public BoundingBox Expand(double paddingKm)
    {
        if (paddingKm <= 0) return this;

        var dLat = paddingKm / 111.0;
        var centerLat = CenterLatitude;
        var cosLat = Math.Max(0.000001, Math.Cos(centerLat * Math.PI / 180.0));
        var dLon = paddingKm / (111.0 * cosLat);

        return new BoundingBox(
            Math.Max(-90.0, MinLatitude - dLat),
            Math.Min(90.0, MaxLatitude + dLat),
            Math.Max(-180.0, MinLongitude - dLon),
            Math.Min(180.0, MaxLongitude + dLon)
        );
    }

    public static bool IsValid(BoundingBox? box)
    {
        if (box is null) return false;
        return box.MinLatitude < box.MaxLatitude &&
               box.MinLongitude < box.MaxLongitude &&
               box.MinLatitude >= -90.0 && box.MaxLatitude <= 90.0 &&
               box.MinLongitude >= -180.0 && box.MaxLongitude <= 180.0;
    }
}
