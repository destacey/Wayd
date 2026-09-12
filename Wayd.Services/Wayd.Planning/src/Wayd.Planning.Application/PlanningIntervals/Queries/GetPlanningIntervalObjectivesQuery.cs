using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalObjectivesQuery : IQuery<IReadOnlyList<PlanningIntervalObjectiveListDto>>
{
    public GetPlanningIntervalObjectivesQuery(IdOrKey idOrKey, Guid? teamId)
    {
        PlanningIntervalIdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
        TeamId = teamId;
    }

    public Expression<Func<PlanningInterval, bool>> PlanningIntervalIdOrKeyFilter { get; }
    public Guid? TeamId { get; set; }
}

public sealed class GetPlanningIntervalObjectivesQueryHandler(IPlanningDbContext planningDbContext, ILogger<GetPlanningIntervalObjectivesQueryHandler> logger, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetPlanningIntervalObjectivesQuery, IReadOnlyList<PlanningIntervalObjectiveListDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly ILogger<GetPlanningIntervalObjectivesQueryHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IReadOnlyList<PlanningIntervalObjectiveListDto>> Handle(GetPlanningIntervalObjectivesQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.PlanningIntervals.AsQueryable();

        if (request.TeamId.HasValue)
        {
            var piTeamExists = await _planningDbContext.PlanningIntervals
                .Where(request.PlanningIntervalIdOrKeyFilter)
                .AnyAsync(p => p.Teams.Any(t => t.TeamId == request.TeamId.Value), cancellationToken);
            if (!piTeamExists)
            {
                ThrowAndLogException(request, $"Planning interval does not have team {request.TeamId}.");
            }

            query = query
                .Include(p => p.Objectives.Where(o => o.TeamId == request.TeamId.Value))
                    .ThenInclude(o => o.Team)
                .Include(p => p.Objectives.Where(o => o.TeamId == request.TeamId.Value))
                    .ThenInclude(o => o.HealthChecks)
                        .ThenInclude(h => h.ReportedBy);
        }
        else
        {
            query = query
                .Include(p => p.Objectives)
                    .ThenInclude(o => o.Team)
                .Include(p => p.Objectives)
                    .ThenInclude(o => o.HealthChecks)
                        .ThenInclude(h => h.ReportedBy);
        }

        var planningInterval = await query
            .Where(request.PlanningIntervalIdOrKeyFilter)
            .AsNoTrackingWithIdentityResolution()
            .FirstOrDefaultAsync(cancellationToken);
        if (planningInterval is null || planningInterval.Objectives.Count == 0)
            return [];

        var piNavigation = NavigationDto.Create(planningInterval.Id, planningInterval.Key, planningInterval.Name);
        var now = _dateTimeProvider.Now;

        return planningInterval.Objectives
            .Select(o => PlanningIntervalObjectiveListDto.Create(o, piNavigation, now))
            .ToList();
    }

    private void ThrowAndLogException(GetPlanningIntervalObjectivesQuery request, string message)
    {
        var requestName = request.GetType().Name;
        var exception = new InternalServerException(message);

        _logger.LogError(exception, "Wayd Request: Exception for Request {Name} {@Request}. {Message}", requestName, request, message);
        throw exception;
    }
}
