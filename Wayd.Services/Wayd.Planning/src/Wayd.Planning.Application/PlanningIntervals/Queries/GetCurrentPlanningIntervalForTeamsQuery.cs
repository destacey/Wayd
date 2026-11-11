using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PlanningIntervals.Dtos;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

/// <summary>
/// The planning interval running on <paramref name="Today"/> that any of <paramref name="TeamIds"/> takes part in,
/// with the teams' objectives at risk in it; null when none is running. Where several are, one with
/// <paramref name="PreferredTeamId"/> in it comes first, then the one that started first.
/// </summary>
/// <param name="PreferredTeamId">The team the interval is wanted for, such as a team of teams over <paramref name="TeamIds"/>.</param>
/// <param name="TeamIds">Every team whose objectives count, <paramref name="PreferredTeamId"/> included.</param>
/// <param name="IncludeObjectives">Whether to count the objectives at risk; false for a caller who may not view objectives.</param>
public sealed record GetCurrentPlanningIntervalForTeamsQuery(Guid PreferredTeamId, IReadOnlyCollection<Guid> TeamIds, LocalDate Today, bool IncludeObjectives) : IQuery<CurrentPlanningIntervalDto?>;

/// <summary>Handles <see cref="GetCurrentPlanningIntervalForTeamsQuery"/>.</summary>
public sealed class GetCurrentPlanningIntervalForTeamsQueryHandler(IPlanningDbContext planningDbContext, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetCurrentPlanningIntervalForTeamsQuery, CurrentPlanningIntervalDto?>
{
    private static readonly ObjectiveStatus[] _openStatuses = [ObjectiveStatus.NotStarted, ObjectiveStatus.InProgress];

    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc />
    public async Task<CurrentPlanningIntervalDto?> Handle(GetCurrentPlanningIntervalForTeamsQuery request, CancellationToken cancellationToken)
    {
        var teamIds = request.TeamIds.Append(request.PreferredTeamId).Distinct().ToList();
        var today = request.Today;

        var planningInterval = await _planningDbContext.PlanningIntervals
            .Where(p => p.DateRange.Start <= today && today <= p.DateRange.End)
            .Where(p => p.Teams.Any(t => teamIds.Contains(t.TeamId)))
            .OrderByDescending(p => p.Teams.Any(t => t.TeamId == request.PreferredTeamId))
            .ThenBy(p => p.DateRange.Start)
            .ThenBy(p => p.Key)
            .Select(p => new { p.Id, p.Key, p.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (planningInterval is null)
            return null;

        var navigation = new NavigationDto { Id = planningInterval.Id, Key = planningInterval.Key, Name = planningInterval.Name };
        if (!request.IncludeObjectives)
            return new CurrentPlanningIntervalDto { PlanningInterval = navigation };

        var now = _dateTimeProvider.Now;
        var currentHealth = await _planningDbContext.PlanningIntervalObjectives
            .Where(o => o.PlanningIntervalId == planningInterval.Id && teamIds.Contains(o.TeamId) && _openStatuses.Contains(o.Status))
            .Select(o => o.HealthChecks
                .Where(h => h.Expiration > now)
                .OrderByDescending(h => h.ReportedOn)
                .Select(h => (HealthStatus?)h.Status)
                .FirstOrDefault())
            .ToListAsync(cancellationToken);

        return new CurrentPlanningIntervalDto
        {
            PlanningInterval = navigation,
            ObjectivesAtRiskCount = currentHealth.Count(h => h is HealthStatus.AtRisk or HealthStatus.Unhealthy),
        };
    }
}
