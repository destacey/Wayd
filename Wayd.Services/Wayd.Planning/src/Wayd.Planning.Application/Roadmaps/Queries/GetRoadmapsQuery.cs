using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Roadmaps.Dtos;

namespace Wayd.Planning.Application.Roadmaps.Queries;

public sealed record GetRoadmapsQuery(RoadmapState[]? StateFilter = null) : IQuery<List<RoadmapListDto>>;

public sealed class GetRoadmapsQueryHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetRoadmapsQuery, List<RoadmapListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<List<RoadmapListDto>> Handle(GetRoadmapsQuery request, CancellationToken cancellationToken)
    {
        var publicVisibility = Visibility.Public;

        // Unlinked callers manage nothing and see public roadmaps only, rather than the list failing
        // outright. The manager check is omitted rather than run against a sentinel id.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

        var query = employeeId is { } managerId
            ? _planningDbContext.Roadmaps.Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == managerId))
            : _planningDbContext.Roadmaps.Where(r => r.Visibility == publicVisibility);

        if (request.StateFilter is { Length: > 0 })
        {
            query = query.Where(r => request.StateFilter.Contains(r.State));
        }

        return await query
            .ProjectToType<RoadmapListDto>()
            .ToListAsync(cancellationToken);
    }
}
