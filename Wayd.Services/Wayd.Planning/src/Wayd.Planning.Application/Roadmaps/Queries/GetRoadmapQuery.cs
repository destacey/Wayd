using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Roadmaps.Queries;

public sealed record GetRoadmapQuery : IQuery<RoadmapDetailsDto?>
{
    public GetRoadmapQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Roadmap>();
    }

    public Expression<Func<Roadmap, bool>> IdOrKeyFilter { get; }
}

public sealed class GetRoadmapQueryHandler(IPlanningDbContext planningDbContext, ICurrentPrincipal currentPrincipal) : IQueryHandler<GetRoadmapQuery, RoadmapDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<RoadmapDetailsDto?> Handle(GetRoadmapQuery request, CancellationToken cancellationToken)
    {
        var publicVisibility = Visibility.Public;

        // A caller with no employee link manages nothing, so they see public roadmaps only — this
        // used to throw, failing a viewer query outright for anyone unlinked. The manager check is
        // omitted rather than run against a sentinel id, so the SQL says what is meant and no
        // always-empty subquery is evaluated.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);

        var query = _planningDbContext.Roadmaps.Where(request.IdOrKeyFilter);

        query = employeeId is { } managerId
            ? query.Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == managerId))
            : query.Where(r => r.Visibility == publicVisibility);

        return await query
            .ProjectToType<RoadmapDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
