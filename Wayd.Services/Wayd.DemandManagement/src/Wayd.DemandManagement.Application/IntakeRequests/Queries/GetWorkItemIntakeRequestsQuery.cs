using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// The intake requests a work item delivers, that the current user may see, by number.
/// </summary>
/// <remarks>
/// The link lives in Demand Management, so the work item page asks here rather than Work knowing about
/// requests. Each request goes through the same read rules as every other read of it: Work decides who sees
/// the work item and knows nothing of desks, so a request on a private desk the reader is not part of is simply
/// absent — no row and no count — rather than named or redacted.
/// </remarks>
/// <param name="WorkItemId">The work item.</param>
public sealed record GetWorkItemIntakeRequestsQuery(Guid WorkItemId) : IQuery<IReadOnlyList<WorkItemIntakeRequestDto>>;

public sealed class GetWorkItemIntakeRequestsQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetWorkItemIntakeRequestsQuery, IReadOnlyList<WorkItemIntakeRequestDto>>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IReadOnlyList<WorkItemIntakeRequestDto>> Handle(GetWorkItemIntakeRequestsQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        return await _dbContext.IntakeRequests
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks))
            .SelectMany(r => r.Outcomes.Where(o => o.Kind == IntakeRequestOutcomeKind.WorkItem && o.TargetId == request.WorkItemId),
                (r, o) => new { r, o })
            .OrderBy(x => x.r.Key)
            .Select(x => new WorkItemIntakeRequestDto
            {
                Request = new IntakeRequestNavigationDto { Id = x.r.Id, Key = x.r.Key, Title = x.r.Title },
                Status = new StatusNavigationDto { Id = x.r.StatusId, Name = x.r.StatusName, Category = x.r.StatusCategory, CategoryLabel = WorkflowOwners.CategoryLabel(DemandManagementWorkflowOwners.IntakeRequest.Key, x.r.StatusCategory), Alias = x.r.StatusAliasValue },
                Note = x.o.Note,
            })
            .ToListAsync(cancellationToken);
    }
}
