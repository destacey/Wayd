using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Extensions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItemDependencies.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Gets the dependencies that cross team lines where one of the given teams owns the predecessor or the successor.
/// </summary>
/// <param name="TeamIds">The teams an end must belong to; the other end may belong to any team, or to none.</param>
/// <param name="DependencyStates">The states to return, matched on the predecessor's status category.</param>
public sealed record GetTeamsDependenciesQuery(IEnumerable<Guid> TeamIds, IEnumerable<DependencyState> DependencyStates) : IQuery<List<DependencyDto>>;

/// <summary>
/// Handles <see cref="GetTeamsDependenciesQuery"/>.
/// </summary>
public sealed class GetTeamsDependenciesQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetTeamsDependenciesQuery, List<DependencyDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <summary>
    /// Returns the matching dependencies, oldest first. Intra-Team dependencies are left out; a dependency with an end
    /// that has no team is kept, because its scope is unknown rather than known to be within one team.
    /// </summary>
    public async Task<List<DependencyDto>> Handle(GetTeamsDependenciesQuery request, CancellationToken cancellationToken)
    {
        var teamIds = request.TeamIds.Distinct().ToArray();
        if (teamIds.Length == 0)
            return [];

        var statusCategories = request.DependencyStates.Select(d => d.ToStatusCategory()).Distinct().ToArray();
        if (statusCategories.Length == 0)
            return [];

        return await _workDbContext.WorkItemDependencies
            .Where(d => d.RemovedOn == null)
            .Where(d => statusCategories.Contains(d.Source!.StatusCategory))
            .Where(d => teamIds.Contains(d.Source!.TeamId!.Value) || teamIds.Contains(d.Target!.TeamId!.Value))
            .Where(d => d.Source!.TeamId == null || d.Target!.TeamId == null || d.Source.TeamId != d.Target.TeamId)
            .OrderBy(d => d.CreatedOn)
            .ThenBy(d => d.Id)
            .ProjectToType<DependencyDto>()
            .ToListAsync(cancellationToken);
    }
}
