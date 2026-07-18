using System.Linq.Expressions;
using Wayd.Common.Application.Models.Organizations;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Extensions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetTeamWorkItemsQuery : IQuery<List<WorkItemListDto>>
{
    public GetTeamWorkItemsQuery(TeamIdOrCode teamIdOrCode, WorkStatusCategory[]? statusCategories = null, Instant? doneFrom = null, Instant? doneTo = null)
    {
        TeamFilter = teamIdOrCode.CreateWorkTeamFilter<WorkItem>();
        StatusCategories = statusCategories ?? [];
        DoneFrom = doneFrom;
        DoneTo = doneTo;
    }

    public Expression<Func<WorkItem, bool>> TeamFilter { get; }
    public WorkStatusCategory[] StatusCategories { get; }
    public Instant? DoneFrom { get; }
    public Instant? DoneTo { get; }
}

public sealed class GetTeamWorkItemsQueryHandler(
    IWorkDbContext workDbContext)
    : IQueryHandler<GetTeamWorkItemsQuery, List<WorkItemListDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<List<WorkItemListDto>> Handle(GetTeamWorkItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkItems
            .Where(request.TeamFilter)
            .Where(item => item.Type.Level!.Tier == WorkTypeTier.Requirement);

        if (request.StatusCategories.Length != 0)
        {
            query = query.Where(item => request.StatusCategories.Contains(item.StatusCategory));
        }

        if (request.DoneFrom.HasValue)
        {
            query = query.Where(item => item.DoneTimestamp.HasValue && item.DoneTimestamp >= request.DoneFrom.Value);
        }

        if (request.DoneTo.HasValue)
        {
            query = query.Where(item => item.DoneTimestamp.HasValue && item.DoneTimestamp <= request.DoneTo.Value);
        }

        var workItems = await query
            .ProjectToType<WorkItemListDto>()
            .ToListAsync(cancellationToken);

        return workItems;
    }
}