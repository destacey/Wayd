using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetTeamPlanningIntervalPredictabilityQuery : IQuery<double?>
{
    public GetTeamPlanningIntervalPredictabilityQuery(IdOrKey idOrKey, Guid teamId)
    {
        PlanningIntervalIdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
        TeamId = teamId;
    }

    public Expression<Func<PlanningInterval, bool>> PlanningIntervalIdOrKeyFilter { get; }
    public Guid TeamId { get; }
}

internal sealed class GetTeamPlanningIntervalPredictabilityQueryHandler(IPlanningDbContext planningDbContext, ILogger<GetTeamPlanningIntervalPredictabilityQueryHandler> logger, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetTeamPlanningIntervalPredictabilityQuery, double?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<GetTeamPlanningIntervalPredictabilityQueryHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<double?> Handle(GetTeamPlanningIntervalPredictabilityQuery request, CancellationToken cancellationToken)
    {
        // TODO: filter by team on teams and objectives
        var planningInterval = await _planningDbContext.PlanningIntervals
            .Include(p => p.Teams.Where(t => t.TeamId == request.TeamId && t.Team.Type == TeamType.Team))
            .Include(p => p.Objectives.Where(o => o.TeamId == request.TeamId && o.Type == PlanningIntervalObjectiveType.Team))
            .Where(request.PlanningIntervalIdOrKeyFilter)
            .AsNoTrackingWithIdentityResolution()
            .FirstOrDefaultAsync(cancellationToken);
        if (planningInterval is null || planningInterval.Teams.Count == 0)
            return null;

        return planningInterval.CalculatePredictability(_dateTimeProvider.Today, request.TeamId);
    }
}

