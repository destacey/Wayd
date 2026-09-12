using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalObjectiveQuery : IQuery<PlanningIntervalObjectiveDetailsDto?>
{
    public GetPlanningIntervalObjectiveQuery(IdOrKey idOrKey, IdOrKey objectiveIdOrKey)
    {
        PlanningIntervalIdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
        ObjectiveIdOrKeyFilter = objectiveIdOrKey.CreateFilter<PlanningIntervalObjective>();
        ObjectiveIdOrKey = objectiveIdOrKey;
    }

    public Expression<Func<PlanningInterval, bool>> PlanningIntervalIdOrKeyFilter { get; }
    public Expression<Func<PlanningIntervalObjective, bool>> ObjectiveIdOrKeyFilter { get; }

    public IdOrKey ObjectiveIdOrKey { get; }
}

public sealed class GetPlanningIntervalObjectiveQueryHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetPlanningIntervalObjectiveQuery, PlanningIntervalObjectiveDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<PlanningIntervalObjectiveDetailsDto?> Handle(GetPlanningIntervalObjectiveQuery request, CancellationToken cancellationToken)
    {
        Guid? objectiveId = request.ObjectiveIdOrKey.AsId;
        if (objectiveId is null)
        {
            objectiveId = await GetObjectiveId(request, cancellationToken);
            if (objectiveId == Guid.Empty || objectiveId is null)
                return null;
        }

        var planningInterval = await _planningDbContext.PlanningIntervals
            .Include(p => p.Objectives.Where(o => o.Id == objectiveId.Value))
                .ThenInclude(o => o.Team)
            .Include(p => p.Objectives.Where(o => o.Id == objectiveId.Value))
                .ThenInclude(o => o.HealthChecks)
                    .ThenInclude(h => h.ReportedBy)
            .Where(request.PlanningIntervalIdOrKeyFilter)
            .AsNoTrackingWithIdentityResolution()
            .FirstOrDefaultAsync(cancellationToken);
        if (planningInterval is null || planningInterval.Objectives.Count != 1
            || planningInterval.Objectives.First().Id != objectiveId.Value)
            return null;

        var piNavigation = NavigationDto.Create(planningInterval.Id, planningInterval.Key, planningInterval.Name);

        return PlanningIntervalObjectiveDetailsDto.Create(planningInterval.Objectives.First(), piNavigation, _dateTimeProvider.Now);
    }

    private async Task<Guid?> GetObjectiveId(GetPlanningIntervalObjectiveQuery request, CancellationToken cancellationToken)
    {
        return await _planningDbContext.PlanningIntervals
            .Where(request.PlanningIntervalIdOrKeyFilter)
            .SelectMany(p => p.Objectives)
            .Where(request.ObjectiveIdOrKeyFilter)
            .Select(o => o.Id) // returns empty guid if no match
            .FirstOrDefaultAsync(cancellationToken);
    }
}
