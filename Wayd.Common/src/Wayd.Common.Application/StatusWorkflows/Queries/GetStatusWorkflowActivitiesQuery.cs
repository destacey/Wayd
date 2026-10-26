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
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetStatusWorkflowActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IStatusWorkflowDbContext _statusWorkflowDbContext = statusWorkflowDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetStatusWorkflowActivitiesQuery request, CancellationToken cancellationToken)
    {
        var workflowId = await _statusWorkflowDbContext.StatusWorkflows
            .Where(request.IdOrKeyFilter)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (workflowId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        // The events name the aggregate "Workflow" rather than the entity's own StatusWorkflow.
        var activities = await _activityLogReader.Read(
            workflowId.Value,
            aggregateType: "Workflow",
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
