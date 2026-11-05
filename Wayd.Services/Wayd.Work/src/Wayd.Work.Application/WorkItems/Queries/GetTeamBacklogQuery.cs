using System.Linq.Expressions;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.Extensions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetTeamBacklogQuery : IQuery<Result<List<WorkItemBacklogItemDto>>>
{
    public GetTeamBacklogQuery(TeamIdOrCode teamIdOrCode)
    {
        TeamFilter = teamIdOrCode.CreateWorkTeamFilter<WorkItem>();
    }

    public Expression<Func<WorkItem, bool>> TeamFilter { get; }
}

public sealed class GetTeamBacklogQueryHandler(IWorkDbContext workDbContext) : IQueryHandler<GetTeamBacklogQuery, Result<List<WorkItemBacklogItemDto>>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<Result<List<WorkItemBacklogItemDto>>> Handle(GetTeamBacklogQuery request, CancellationToken cancellationToken)
    {
        var validStatusCategories = new[]
        {
            StatusCategory.Proposed,
            StatusCategory.Active
        };

        var query = _workDbContext.WorkItems
            .Where(request.TeamFilter)
            .Where(item => item.Type.Level!.Tier == WorkTypeTier.Requirement)
            .Where(w => validStatusCategories.Contains(w.StatusCategory));

        var workItems = await query
            .ProjectToType<WorkItemBacklogItemDto>()
            .ToListAsync(cancellationToken);

        workItems.Sort((a, b) =>
        {
            var rankCompare = a.StackRank.CompareTo(b.StackRank);
            if (rankCompare != 0)
                return rankCompare;

            var createdCompare = a.Created.CompareTo(b.Created);
            return createdCompare != 0 ? createdCompare : a.Id.CompareTo(b.Id);
        });

        foreach (var (index, workItem) in workItems.Index())
        {
            workItem.Rank = index + 1;
        }

        return workItems;
    }
}