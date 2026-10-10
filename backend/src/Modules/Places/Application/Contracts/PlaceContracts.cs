using Places.Domain.Entities;

namespace Places.Application.Contracts;

public sealed record PlaceResponse(
    Guid Id,
    string ExternalId,
    string Name,
    string? Address,
    double Latitude,
    double Longitude,
    string? OpeningHours,
    string? Category,
    string Source)
{
    public static PlaceResponse FromEntity(Place p) => new(
        p.Id,
        p.ExternalId,
        p.Name,
        p.Address,
        p.Latitude,
        p.Longitude,
        p.OpeningHours,
        p.Category,
        p.Source);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record CreatePlaceCommand(
    string Name,
    double Latitude,
    double Longitude,
    string? Address = null,
    string? Category = null,
    string? OpeningHours = null,
    string? Source = null,
    string? ExternalId = null);

public sealed record UpdatePlaceCommand(
    Guid Id,
    string Name,
    double Latitude,
    double Longitude,
    string? Address = null,
    string? Category = null,
    string? OpeningHours = null,
    string? Source = null);
