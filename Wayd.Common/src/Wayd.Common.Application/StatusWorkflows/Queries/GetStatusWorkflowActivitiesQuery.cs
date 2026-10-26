using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows.Queries;

/// <summary>
/// Retrieves activity history for a status workflow, newest first.
/// </summary>
public sealed record GetStatusWorkflowActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetStatusWorkflowActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<StatusWorkflow>();
        Paging = paging;
    }

    public Expression<Func<StatusWorkflow, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetStatusWorkflowActivitiesQueryValidator : PagedQueryValidator<GetStatusWorkflowActivitiesQuery>;

public sealed class GetStatusWorkflowActivitiesQueryHandler(
    IStatusWorkflowDbContext statusWorkflowDbContext,
    IActivityLogReader activityLogReader,
    IWorkflowOwnerAvailability ownerAvailability)
    : IQueryHandler<GetStatusWorkflowActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;
    private readonly IWorkflowOwnerAvailability _ownerAvailability = ownerAvailability;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetStatusWorkflowActivitiesQuery request, CancellationToken cancellationToken)
    {
        var workflow = await _statusWorkflowDbContext.StatusWorkflows
            .Where(request.IdOrKeyFilter)
            .Select(w => new { w.Id, w.OwnerType })
            .FirstOrDefaultAsync(cancellationToken);

        if (workflow is null || !await _ownerAvailability.IsAvailable(workflow.OwnerType, cancellationToken))
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        // The events name the aggregate "Workflow" rather than the entity's own StatusWorkflow.
        var activities = await _activityLogReader.Read(
            workflow.Id,
            aggregateType: "Workflow",
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
