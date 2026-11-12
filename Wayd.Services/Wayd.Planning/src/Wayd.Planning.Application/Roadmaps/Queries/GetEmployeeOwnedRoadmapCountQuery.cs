using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.Roadmaps.Queries;

/// <summary>
/// How many active roadmaps an employee is an owner of: the roadmaps <see cref="GetRoadmapsQuery"/> lists for them
/// with <c>OwnedByMe</c> and the Active state.
/// </summary>
public sealed record GetEmployeeOwnedRoadmapCountQuery(Guid EmployeeId) : IQuery<int>;

public sealed class GetEmployeeOwnedRoadmapCountQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetEmployeeOwnedRoadmapCountQuery, int>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public Task<int> Handle(GetEmployeeOwnedRoadmapCountQuery request, CancellationToken cancellationToken) =>
        _planningDbContext.Roadmaps
            .CountAsync(r => r.State == RoadmapState.Active && r.Owners.Any(o => o.EmployeeId == request.EmployeeId), cancellationToken);
}
