using System.Linq.Expressions;
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

public sealed class GetRoadmapActivitiesQueryHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal) : IQueryHandler<GetRoadmapActivitiesQuery, List<RoadmapActivityListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<RoadmapActivityListDto>> Handle(GetRoadmapActivitiesQuery request, CancellationToken cancellationToken)
    {
        var publicVisibility = Visibility.Public;

        // Unlinked callers manage nothing and so see public roadmaps only. The manager check is
        // omitted rather than run against a sentinel id.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

        var roadmaps = _planningDbContext.Roadmaps
            .AsNoTracking()
            .Where(request.IdOrKeyFilter);

        roadmaps = employeeId is { } managerId
            ? roadmaps.Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == managerId))
            : roadmaps.Where(r => r.Visibility == publicVisibility);

        var items = await roadmaps
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
