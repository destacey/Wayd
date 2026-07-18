using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Planning.Application.Models;
using Wayd.Planning.Domain.Enums;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

public sealed record GetPlanningIntervalPredictabilityQuery : IQuery<PlanningIntervalPredictabilityDto?>
{
    public GetPlanningIntervalPredictabilityQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
}

public sealed class GetPlanningIntervalPredictabilityQueryHandler : IQueryHandler<GetPlanningIntervalPredictabilityQuery, PlanningIntervalPredictabilityDto?>
{
    private readonly IPlanningDbContext _planningDbContext;
    private readonly ILogger<GetPlanningIntervalPredictabilityQueryHandler> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetPlanningIntervalPredictabilityQueryHandler(IPlanningDbContext planningDbContext, ILogger<GetPlanningIntervalPredictabilityQueryHandler> logger, IDateTimeProvider dateTimeProvider)
    {
        _planningDbContext = planningDbContext;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PlanningIntervalPredictabilityDto?> Handle(GetPlanningIntervalPredictabilityQuery request, CancellationToken cancellationToken)
    {
        // TODO: filter by teams only.  Don't include teams or objective data for team of teams.
        var planningInterval = await _planningDbContext.PlanningIntervals
            .Include(p => p.Teams.Where(t => t.Team.Type == TeamType.Team))
                .ThenInclude(t => t.Team)
            .Include(p => p.Objectives.Where(o => o.Type == PlanningIntervalObjectiveType.Team))
            .AsNoTracking()
            .FirstOrDefaultAsync(request.IdOrKeyFilter, cancellationToken);

        if (planningInterval is null)
            return null;
        else if (!planningInterval.Teams.Any())
            return new PlanningIntervalPredictabilityDto(null, []);

        var currentDate = _dateTimeProvider.Today;

        // Pre-bucket team objectives once. We need per-team counts of
        // regular/stretch/completed objectives — same data the predictability
        // calculation already uses, just exposed for display.
        var objectivesByTeam = planningInterval.Objectives
            .GroupBy(o => o.TeamId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var teamPredictabilities = planningInterval.Teams
            .Select(t =>
            {
                objectivesByTeam.TryGetValue(t.TeamId, out var teamObjectives);
                teamObjectives ??= [];
                return new PlanningIntervalTeamPredictabilityDto
                {
                    Team = PlanningTeamNavigationDto.FromPlanningTeam(t.Team),
                    Predictability = planningInterval.CalculatePredictability(currentDate, t.TeamId),
                    RegularObjectivesCount = teamObjectives.Count(o => !o.IsStretch),
                    StretchObjectivesCount = teamObjectives.Count(o => o.IsStretch),
                    CompletedObjectivesCount = teamObjectives.Count(o => o.Status == ObjectiveStatus.Completed),
                };
            })
            .OrderBy(t => t.Team.Name)
            .ToList();

        return new PlanningIntervalPredictabilityDto(planningInterval.CalculatePredictability(currentDate), teamPredictabilities);
    }
}

public sealed record PlanningIntervalPredictabilityDto(double? Predictability, List<PlanningIntervalTeamPredictabilityDto> TeamPredictabilities);

public sealed record PlanningIntervalTeamPredictabilityDto
{
    public required PlanningTeamNavigationDto Team { get; init; }
    public double? Predictability { get; init; }

    /// <summary>Count of non-stretch (committed) objectives for this team.</summary>
    public int RegularObjectivesCount { get; init; }
    public int StretchObjectivesCount { get; init; }
    public int CompletedObjectivesCount { get; init; }
}
