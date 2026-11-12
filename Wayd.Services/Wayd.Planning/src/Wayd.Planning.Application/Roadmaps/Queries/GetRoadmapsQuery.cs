using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Roadmaps.Dtos;

namespace Wayd.Planning.Application.Roadmaps.Queries;

/// <summary>
/// The roadmaps the caller can see, ordered by key.
/// </summary>
/// <param name="StateFilter">Only roadmaps in one of these states; every state when null or empty.</param>
/// <param name="OwnedByMe">
/// Only roadmaps the caller's linked employee is an owner of; none when the account has no linked employee.
/// </param>
public sealed record GetRoadmapsQuery(RoadmapState[]? StateFilter = null, bool OwnedByMe = false) : IQuery<List<RoadmapListDto>>;

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

        if (request.OwnedByMe)
        {
            if (reader.EmployeeId is not { } employeeId)
                return [];

            query = query.Where(r => r.Owners.Any(o => o.EmployeeId == employeeId));
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
