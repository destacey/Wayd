using System.Linq.Expressions;
using Ardalis.GuardClauses;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Queries;

public sealed record GetRoadmapActivitiesQuery : IQuery<List<RoadmapActivityListDto>>
{
    public GetRoadmapActivitiesQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Roadmap>();
    }

    public Expression<Func<Roadmap, bool>> IdOrKeyFilter { get; }
}

public sealed class GetRoadmapActivitiesQueryHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser) : IQueryHandler<GetRoadmapActivitiesQuery, List<RoadmapActivityListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());

    public async Task<List<RoadmapActivityListDto>> Handle(GetRoadmapActivitiesQuery request, CancellationToken cancellationToken)
    {
        var publicVisibility = Visibility.Public;

        var items = await _planningDbContext.Roadmaps
            .Where(request.IdOrKeyFilter)
            .Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == _currentUserEmployeeId))
            .SelectMany(r => r.Items)
            .Where(ri => ri.Type == RoadmapItemType.Activity)
            .OfType<RoadmapActivity>()
            //.ProjectToType<RoadmapActivityListDto>() // not working, it's always returning only the BaseRoadmapItem properties
            .ToListAsync(cancellationToken);

        return items
            .Where(ri => ri.Parent == null)
            .OrderBy(ri => ri.Order)
            //.ToList();
            .Adapt<List<RoadmapActivityListDto>>();
    }
}
