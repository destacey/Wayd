using Wayd.Common.Models;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetChildWorkItemsQuery : IQuery<Result<IReadOnlyCollection<WorkItemListDto>>>
{
    public GetChildWorkItemsQuery(Guid workspaceId, WorkItemKey workItemKey)
    {
        Id = Guard.Against.NullOrEmpty(workspaceId);
        WorkItemKey = workItemKey;
    }

    public GetChildWorkItemsQuery(WorkspaceKey workspaceKey, WorkItemKey workItemKey)
    {
        Key = Guard.Against.Default(workspaceKey);
        WorkItemKey = workItemKey;
    }

    public Guid? Id { get; }
    public WorkspaceKey? Key { get; }
    public WorkItemKey WorkItemKey { get; }
}

public sealed class GetChildWorkItemsQueryHandler(IWorkDbContext workDbContext, ILogger<GetChildWorkItemsQueryHandler> logger) : IQueryHandler<GetChildWorkItemsQuery, Result<IReadOnlyCollection<WorkItemListDto>>>
{
    private const string AppRequestName = nameof(GetChildWorkItemsQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetChildWorkItemsQueryHandler> _logger = logger;

    public async Task<Result<IReadOnlyCollection<WorkItemListDto>>> Handle(GetChildWorkItemsQuery request, CancellationToken cancellationToken)
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
            return Result.Failure<IReadOnlyCollection<WorkItemListDto>>("No valid workspace id or key provided.");
        }

        var workItemId = await query.Select(e => e.Id).FirstOrDefaultAsync(cancellationToken);
        if (workItemId.IsDefault())
        {
            _logger.LogError("{AppRequestName}: Work item not found. {@Request}", AppRequestName, request);
            return Result.Failure<IReadOnlyCollection<WorkItemListDto>>("Work item not found.");
        }

        return await _workDbContext.WorkItems
            .Where(w => w.ParentId == workItemId)
            .ProjectToType<WorkItemListDto>()
            .ToListAsync(cancellationToken);
    }
}
