using System.Linq.Expressions;
using Wayd.Common.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalTeamsQuery : IQuery<IReadOnlyList<Guid>>
{
    public GetPlanningIntervalTeamsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
}

public sealed class GetPlanningIntervalTeamsQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetPlanningIntervalTeamsQuery, IReadOnlyList<Guid>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<Guid>> Handle(GetPlanningIntervalTeamsQuery request, CancellationToken cancellationToken)
    {
        return await _planningDbContext.PlanningIntervals
            .Where(request.IdOrKeyFilter)
            .SelectMany(p => p.Teams.Select(t => t.TeamId))
            .ToListAsync(cancellationToken);
    }
}
