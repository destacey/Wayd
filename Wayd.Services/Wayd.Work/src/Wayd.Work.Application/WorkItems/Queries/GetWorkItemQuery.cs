using Wayd.Common.Models;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetWorkItemQuery : IQuery<Result<WorkItemDetailsDto?>>
{
    public GetWorkItemQuery(Guid workspaceId, WorkItemKey workItemKey)
    {
        Id = Guard.Against.NullOrEmpty(workspaceId);
        WorkItemKey = workItemKey;
    }

    public GetWorkItemQuery(WorkspaceKey workspaceKey, WorkItemKey workItemKey)
    {
        Key = Guard.Against.Default(workspaceKey);
        WorkItemKey = workItemKey;
    }

    public Guid? Id { get; }
    public WorkspaceKey? Key { get; }
    public WorkItemKey WorkItemKey { get; }
}

public sealed class GetWorkItemQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkItemQueryHandler> logger) : IQueryHandler<GetWorkItemQuery, Result<WorkItemDetailsDto?>>
{
    private const string AppRequestName = nameof(GetWorkItemQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkItemQueryHandler> _logger = logger;

    public async Task<Result<WorkItemDetailsDto?>> Handle(GetWorkItemQuery request, CancellationToken cancellationToken)
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
            return Result.Failure<WorkItemDetailsDto?>("No valid workspace id or key provided.");
        }

        return await query.ProjectToType<WorkItemDetailsDto>().FirstOrDefaultAsync(cancellationToken);
    }
}
