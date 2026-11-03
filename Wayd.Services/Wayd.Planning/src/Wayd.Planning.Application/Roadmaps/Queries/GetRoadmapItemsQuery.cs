using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Queries;

public sealed record GetRoadmapItemsQuery : IQuery<List<RoadmapItemListDto>>
{
    public GetRoadmapItemsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Roadmap>();
    }

    public Expression<Func<Roadmap, bool>> IdOrKeyFilter { get; }
}

public sealed class GetRoadmapItemsQueryHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal) : IQueryHandler<GetRoadmapItemsQuery, List<RoadmapItemListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<RoadmapItemListDto>> Handle(GetRoadmapItemsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveRoadmapReader(cancellationToken);

        var roadmaps = _planningDbContext.Roadmaps.Where(request.IdOrKeyFilter)
            .Where(reader.Filter);

        var items = await roadmaps
            .SelectMany(r => r.Items)
            //.ProjectToType<RoadmapItemDto>() // not working, it's always returning only the BaseRoadmapItem properties
            .ToListAsync(cancellationToken);

        var dtos = items.Adapt<List<RoadmapItemListDto>>();

        return OrderItems([.. dtos.Where(r => r.Parent == null)]);
    }

    private static List<RoadmapItemListDto> OrderItems(List<RoadmapItemListDto> items)
    {
        var orderedItems = items
            .OrderBy(r => r is RoadmapActivityListDto activity ? activity.Order : int.MaxValue)
            .ThenBy(r => r.Id)
            .ToList();

        foreach (var item in orderedItems.OfType<RoadmapActivityListDto>())
        {
            item.Children = OrderItems([.. item.Children]);
        }

        return orderedItems;
    }
}
