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
        var reader = await _currentPrincipal.ResolveRoadmapReader(cancellationToken);

        var query = _planningDbContext.Roadmaps.Where(reader.Filter);

        if (request.StateFilter is { Length: > 0 })
        {
            query = query.Where(r => request.StateFilter.Contains(r.State));
        }

        // Stable, so an MCP caller's offset slices the same list on every call.
        var roadmaps = await query
            .OrderBy(r => r.Key)
            .ProjectToType<RoadmapListDto>()
            .ToListAsync(cancellationToken);

        var managed = await reader.Managed(query, cancellationToken);
        foreach (var roadmap in roadmaps)
            roadmap.CanManage = managed.Contains(roadmap.Id);

        return roadmaps;
    }
}
