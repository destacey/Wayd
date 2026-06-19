using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetEmployeeWorkItemsQuery(
    Guid EmployeeId,
    WorkStatusCategory[]? StatusCategories = null,
    Instant? DoneFrom = null,
    Instant? DoneTo = null)
    : IQuery<List<WorkItemListDto>>;

internal sealed class GetEmployeeWorkItemsQueryHandler(
    IWorkDbContext workDbContext)
    : IQueryHandler<GetEmployeeWorkItemsQuery, List<WorkItemListDto>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;

    public async Task<List<WorkItemListDto>> Handle(GetEmployeeWorkItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkItems
            .Where(item => item.AssignedToId == request.EmployeeId)
            .Where(item => item.Type.Level!.Tier == WorkTypeTier.Requirement);

        if (request.StatusCategories?.Length > 0)
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

        return await query
            .ProjectToType<WorkItemListDto>()
            .ToListAsync(cancellationToken);
    }
}
