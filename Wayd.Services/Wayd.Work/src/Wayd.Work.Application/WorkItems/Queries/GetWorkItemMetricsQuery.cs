using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Models;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetWorkItemMetricsQuery : IQuery<Result<IReadOnlyCollection<WorkItemProgressDailyRollupDto>>>
{
    public GetWorkItemMetricsQuery(Guid workspaceId, WorkItemKey workItemKey)
    {
        Id = Guard.Against.NullOrEmpty(workspaceId);
        WorkItemKey = workItemKey;
    }

    public GetWorkItemMetricsQuery(WorkspaceKey workspaceKey, WorkItemKey workItemKey)
    {
        Key = Guard.Against.Default(workspaceKey);
        WorkItemKey = workItemKey;
    }

    public Guid? Id { get; }
    public WorkspaceKey? Key { get; }
    public WorkItemKey WorkItemKey { get; }
}

public sealed class GetWorkItemMetricsQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkItemMetricsQueryHandler> logger, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetWorkItemMetricsQuery, Result<IReadOnlyCollection<WorkItemProgressDailyRollupDto>>>
{
    private const string AppRequestName = nameof(GetExternalObjectWorkItemMetricsQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkItemMetricsQueryHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<IReadOnlyCollection<WorkItemProgressDailyRollupDto>>> Handle(GetWorkItemMetricsQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkItems
            .Where(w => w.Key == request.WorkItemKey)
            .AsQueryable();

        if (request.Id.HasValue)
        {
            query = query.Where(w => w.WorkspaceId == request.Id);
        }
        else if (request.Key is not null)
        {
            query = query.Where(w => w.Workspace.Key == request.Key);
        }
        else
        {
            _logger.LogError("{AppRequestName}: No workspace id or key provided. {@Request}", AppRequestName, request);
            return Result.Failure<IReadOnlyCollection<WorkItemProgressDailyRollupDto>>("No valid workspace id or key provided.");
        }

        var workItem = await query
            .Select(w => new { w.Id, w.Created, w.DoneTimestamp, w.Type.Level!.Tier })
            .FirstOrDefaultAsync(cancellationToken);
        if (workItem is null)
        {
            _logger.LogError("{AppRequestName}: Work item not found. {@Request}", AppRequestName, request);
            return Result.Failure<IReadOnlyCollection<WorkItemProgressDailyRollupDto>>("Work item not found.");
        }

        var progress = await new WorkItemProgressStateBuilder(_workDbContext, query).Build(cancellationToken);
        if (progress.Count == 0)
        {
            return new List<WorkItemProgressDailyRollupDto>(0);
        }

        var firstCreated = progress.Min(p => p.Created);
        var start = firstCreated < workItem.Created ? firstCreated.ToDateOnly() : workItem.Created.ToDateOnly();

        var end = workItem.DoneTimestamp?.ToDateOnly() ?? _dateTimeProvider.Now.ToDateOnly();
        // if all items are done, use the last done date as the end date if it is later than the work item done date
        if (workItem.DoneTimestamp.HasValue && progress.All(p => p.StatusCategory == WorkStatusCategory.Done))
        {
            var latestChildDone = progress.Max(p => p.DoneTimestamp!.Value).ToDateOnly();
            if (end < latestChildDone)
            {
                end = latestChildDone;
            }
        }

        return WorkItemProgressDailyRollupDto.CreateList(start, end, progress);
    }
}
