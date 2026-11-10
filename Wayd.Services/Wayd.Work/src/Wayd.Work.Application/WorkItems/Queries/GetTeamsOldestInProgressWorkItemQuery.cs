using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

/// <summary>
/// The Requirement-tier work item each of <paramref name="TeamIds"/> has had in progress longest, keyed by team id:
/// the one activated earliest among those in an In Progress-category status now. A team with nothing in progress,
/// or only items with no activation time, is missing.
/// </summary>
public sealed record GetTeamsOldestInProgressWorkItemQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<IReadOnlyDictionary<Guid, InProgressWorkItemDto>>;

/// <summary>Handles <see cref="GetTeamsOldestInProgressWorkItemQuery"/>.</summary>
public sealed class GetTeamsOldestInProgressWorkItemQueryHandler(IWorkDbContext workDbContext)
    : IQueryHandler<GetTeamsOldestInProgressWorkItemQuery, IReadOnlyDictionary<Guid, InProgressWorkItemDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, InProgressWorkItemDto>> Handle(GetTeamsOldestInProgressWorkItemQuery request, CancellationToken cancellationToken)
    {
        var teamIds = request.TeamIds.Distinct().ToList();
        if (teamIds.Count == 0)
            return new Dictionary<Guid, InProgressWorkItemDto>();

        // A team's work in progress is small, so its items are read and the oldest chosen here.
        var items = await _workDbContext.WorkItems
            .Where(w => w.TeamId.HasValue && teamIds.Contains(w.TeamId.Value))
            .Where(w => w.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => w.StatusCategory == StatusCategory.Active)
            .Where(w => w.ActivatedTimestamp != null)
            .Select(w => new
            {
                TeamId = w.TeamId!.Value,
                Item = new InProgressWorkItemDto
                {
                    Id = w.Id,
                    Key = w.Key.ToString(),
                    WorkspaceKey = w.Workspace.Key.ToString(),
                    Title = w.Title,
                    ActivatedOn = w.ActivatedTimestamp!.Value,
                },
            })
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(i => i.TeamId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(i => i.Item).OrderBy(i => i.ActivatedOn).ThenBy(i => i.Key, StringComparer.Ordinal).First());
    }
}
