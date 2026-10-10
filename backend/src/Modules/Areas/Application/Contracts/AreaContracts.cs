using Areas.Domain.Entities;

namespace Areas.Application.Contracts;

public sealed record AreaResponse(
    Guid Id,
    string Name,
    int Level,
    string LevelName,
    Guid? ParentId,
    string? ExternalCode,
    double BboxMinLat,
    double BboxMinLng,
    double BboxMaxLat,
    double BboxMaxLng,
    bool IsActive)
{
    public static AreaResponse FromEntity(Area a) => new(
        a.Id,
        a.Name,
        (int)a.Level,
        a.Level.ToString(),
        a.ParentId,
        a.ExternalCode,
        a.BboxMinLat,
        a.BboxMinLng,
        a.BboxMaxLat,
        a.BboxMaxLng,
        a.IsActive);
}

public sealed record CreateAreaCommand(
    string Name,
    int Level,
    Guid? ParentId,
    string? ExternalCode,
    double BboxMinLat,
    double BboxMinLng,
    double BboxMaxLat,
    double BboxMaxLng,
    bool IsActive = true);

public sealed record UpdateAreaCommand(
    Guid Id,
    string Name,
    Guid? ParentId,
    string? ExternalCode,
    double BboxMinLat,
    double BboxMinLng,
    double BboxMaxLat,
    double BboxMaxLng,
    bool IsActive);

public sealed record AreaTreeNode(
    Guid Id,
    string Name,
    int Level,
    Guid? ParentId,
    IReadOnlyList<AreaTreeNode> Children)
{
    public static AreaTreeNode FromEntity(Area a, IReadOnlyList<AreaTreeNode> children) =>
        new(a.Id, a.Name, (int)a.Level, a.ParentId, children);
}