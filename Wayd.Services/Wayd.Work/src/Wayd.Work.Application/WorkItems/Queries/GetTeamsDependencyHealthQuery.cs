using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Extensions;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItemDependencies.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Counts each team's open dependencies by planning health: the dependencies a team's Dependencies section lists,
/// so a count matches it. Unlike <see cref="GetTeamsDependenciesQuery"/>, a dependency within one team counts.
/// </summary>
/// <param name="TeamIds">The teams to count for; a team with no open dependency is left out of the result.</param>
public sealed record GetTeamsDependencyHealthQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<TeamsDependencyHealthDto>;

/// <summary>Handles <see cref="GetTeamsDependencyHealthQuery"/>.</summary>
public sealed class GetTeamsDependencyHealthQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetTeamsDependencyHealthQuery, TeamsDependencyHealthDto>
{
    /// <summary>The states a team's Dependencies section shows: the predecessor is not yet done.</summary>
    private static readonly StatusCategory[] _openCategories =
        [DependencyState.ToDo.ToStatusCategory(), DependencyState.InProgress.ToStatusCategory()];

    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <inheritdoc />
    public async Task<TeamsDependencyHealthDto> Handle(GetTeamsDependencyHealthQuery request, CancellationToken cancellationToken)
    {
        var teamIds = request.TeamIds.Distinct().ToArray();
        if (teamIds.Length == 0)
            return new TeamsDependencyHealthDto();

        var dependencies = await _workDbContext.WorkItemDependencies
            .Where(d => d.RemovedOn == null)
            .Where(d => _openCategories.Contains(d.Source!.StatusCategory))
            .Where(d => teamIds.Contains(d.Source!.TeamId!.Value) || teamIds.Contains(d.Target!.TeamId!.Value))
            .Select(d => new { d.Id, SourceTeamId = d.Source!.TeamId, TargetTeamId = d.Target!.TeamId, d.Health })
            .ToListAsync(cancellationToken);

        var teams = teamIds
            .Select(teamId => (teamId, Dependencies: dependencies.Where(d => d.SourceTeamId == teamId || d.TargetTeamId == teamId).ToList()))
            .Where(t => t.Dependencies.Count > 0)
            .ToDictionary(
                t => t.teamId,
                t => new TeamDependencyHealthDto
                {
                    OpenCount = t.Dependencies.Count,
                    AtRiskCount = t.Dependencies.Count(d => d.Health == DependencyPlanningHealth.AtRisk),
                    UnhealthyCount = t.Dependencies.Count(d => d.Health == DependencyPlanningHealth.Unhealthy),
                });

        return new TeamsDependencyHealthDto
        {
            Teams = teams,
            AtRiskOrUnhealthyCount = dependencies.Count(d => d.Health is DependencyPlanningHealth.AtRisk or DependencyPlanningHealth.Unhealthy),
        };
    }
}
