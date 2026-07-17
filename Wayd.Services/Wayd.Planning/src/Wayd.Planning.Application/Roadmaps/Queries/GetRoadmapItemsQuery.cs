using System.Linq.Expressions;
using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
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

public sealed class GetRoadmapItemsQueryHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser) : IQueryHandler<GetRoadmapItemsQuery, List<RoadmapItemListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());

    public async Task<List<RoadmapItemListDto>> Handle(GetRoadmapItemsQuery request, CancellationToken cancellationToken)
    {
        var publicVisibility = Visibility.Public;

        var items = await _planningDbContext.Roadmaps
            .Where(request.IdOrKeyFilter)
            .Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == _currentUserEmployeeId))
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
            .ToList();

        foreach (var item in orderedItems.OfType<RoadmapActivityListDto>())
        {
            item.Children = OrderItems([.. item.Children]);
        }

        return orderedItems;
    }
}
