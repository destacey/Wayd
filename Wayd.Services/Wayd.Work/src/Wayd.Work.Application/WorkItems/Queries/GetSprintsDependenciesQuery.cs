using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Extensions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItemDependencies.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// Gets the dependencies that cross team lines where the predecessor or the successor is planned in one of the given sprints.
/// </summary>
/// <param name="SprintIds">The sprints an end must be planned in; the other end may be anywhere.</param>
/// <param name="DependencyStates">The states to return, matched on the predecessor's status category.</param>
public sealed record GetSprintsDependenciesQuery(IEnumerable<Guid> SprintIds, IEnumerable<DependencyState> DependencyStates) : IQuery<List<DependencyDto>>;

/// <summary>
/// Handles <see cref="GetSprintsDependenciesQuery"/>.
/// </summary>
public sealed class GetSprintsDependenciesQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetSprintsDependenciesQuery, List<DependencyDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <summary>
    /// Returns the matching dependencies, oldest first. Intra-Team dependencies are left out; a dependency with an end
    /// that has no team is kept, because its scope is unknown rather than known to be within one team.
    /// </summary>
    public async Task<List<DependencyDto>> Handle(GetSprintsDependenciesQuery request, CancellationToken cancellationToken)
    {
        var sprintIds = request.SprintIds.Distinct().ToArray();
        if (sprintIds.Length == 0)
            return [];

        var statusCategories = request.DependencyStates.Select(d => d.ToStatusCategory()).Distinct().ToArray();
        if (statusCategories.Length == 0)
            return [];

        return await _workDbContext.WorkItemDependencies
            .Where(d => d.RemovedOn == null)
            .Where(d => statusCategories.Contains(d.Source!.StatusCategory))
            .Where(d => sprintIds.Contains(d.Source!.IterationId!.Value) || sprintIds.Contains(d.Target!.IterationId!.Value))
            .Where(d => d.Source!.TeamId == null || d.Target!.TeamId == null || d.Source.TeamId != d.Target.TeamId)
            .OrderBy(d => d.CreatedOn)
            .ThenBy(d => d.Id)
            .ProjectToType<DependencyDto>()
            .ToListAsync(cancellationToken);
    }
}
