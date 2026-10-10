using Areas.Application.Abstractions;
using Areas.Application.Contracts;
using Areas.Domain.Entities;

namespace Areas.Application.Areas.Queries.GetAreas;

public sealed class GetAreasHandler
{
    private readonly IAreaRepository _repo;

    public GetAreasHandler(IAreaRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<AreaResponse>> HandleAsync(
        int? level,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Area> areas;

        if (parentId.HasValue)
            areas = await _repo.GetChildrenAsync(parentId.Value, cancellationToken);
        else if (level.HasValue && Enum.IsDefined(typeof(AreaLevel), level.Value))
            areas = await _repo.GetByLevelAsync((AreaLevel)level.Value, cancellationToken);
        else
            areas = await _repo.GetAllAsync(cancellationToken);

        return areas.Select(AreaResponse.FromEntity).ToList();
    }

    public async Task<IReadOnlyList<AreaTreeNode>> HandleTreeAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repo.GetAllAsync(cancellationToken);
        var byId = all.ToDictionary(a => a.Id);
        var childrenLookup = all
            .Where(a => a.ParentId.HasValue)
            .GroupBy(a => a.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        IReadOnlyList<AreaTreeNode> Build(Area parent)
        {
            var kids = childrenLookup.TryGetValue(parent.Id, out var list)
                ? list
                : new List<Area>();
            var childNodes = kids
                .Select(c => AreaTreeNode.FromEntity(c, Build(c)))
                .ToList();
            return new List<AreaTreeNode>
            {
                AreaTreeNode.FromEntity(parent, childNodes)
            };
        }

        var topLevel = all.Where(a => a.ParentId is null).ToList();
        var nodes = topLevel
            .SelectMany(p => Build(p))
            .ToList();
        return nodes;
    }
}