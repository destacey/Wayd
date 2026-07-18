using Ardalis.GuardClauses;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Roadmaps.Dtos;

namespace Wayd.Planning.Application.Roadmaps.Queries;

public sealed record GetRoadmapsQuery(RoadmapState[]? StateFilter = null) : IQuery<List<RoadmapListDto>>;

public sealed class GetRoadmapsQueryHandler(IPlanningDbContext planningDbContext, ICurrentUser currentUser)
    : IQueryHandler<GetRoadmapsQuery, List<RoadmapListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly Guid _currentUserEmployeeId = Guard.Against.NullOrEmpty(currentUser.GetEmployeeId());

    public async Task<List<RoadmapListDto>> Handle(GetRoadmapsQuery request, CancellationToken cancellationToken)
    {
        var publicVisibility = Visibility.Public;

        var query = _planningDbContext.Roadmaps
            .Where(r => r.Visibility == publicVisibility || r.RoadmapManagers.Any(m => m.ManagerId == _currentUserEmployeeId));

        if (request.StateFilter is { Length: > 0 })
        {
            query = query.Where(r => request.StateFilter.Contains(r.State));
        }

        return await query
            .ProjectToType<RoadmapListDto>()
            .ToListAsync(cancellationToken);
    }
}
