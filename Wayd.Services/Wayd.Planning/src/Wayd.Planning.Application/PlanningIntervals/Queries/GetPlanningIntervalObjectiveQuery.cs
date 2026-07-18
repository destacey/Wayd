using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.Goals.Queries;
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

public sealed class GetPlanningIntervalObjectiveQueryHandler(IPlanningDbContext planningDbContext, ILogger<GetPlanningIntervalObjectiveQueryHandler> logger, IDispatcher dispatcher, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetPlanningIntervalObjectiveQuery, PlanningIntervalObjectiveDetailsDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<GetPlanningIntervalObjectiveQueryHandler> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
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

        // call the objective query handler
        var objective = await _dispatcher.Send(new GetObjectiveForPlanningIntervalQuery(planningInterval.Objectives.First().ObjectiveId, planningInterval.Id), cancellationToken);
        if (objective is null)
            return null;

        var piNavigation = NavigationDto.Create(planningInterval.Id, planningInterval.Key, planningInterval.Name);

        return PlanningIntervalObjectiveDetailsDto.Create(planningInterval.Objectives.First(), objective, piNavigation, _dateTimeProvider.Now);
    }

    // TODO: move this to a repository
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
