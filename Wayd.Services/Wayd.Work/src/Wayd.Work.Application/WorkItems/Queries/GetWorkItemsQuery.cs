using Wayd.Common.Models;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItems.Dtos;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetWorkItemsQuery : IQuery<Result<IReadOnlyCollection<WorkItemListDto>>>
{
    public GetWorkItemsQuery(Guid workspaceId)
    {
        Id = Guard.Against.NullOrEmpty(workspaceId);
    }

    public GetWorkItemsQuery(WorkspaceKey workspaceKey)
    {
        Key = Guard.Against.Default(workspaceKey);
    }

    public Guid? Id { get; }
    public WorkspaceKey? Key { get; }
}

public sealed class GetWorkItemsQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkItemsQueryHandler> logger) : IQueryHandler<GetWorkItemsQuery, Result<IReadOnlyCollection<WorkItemListDto>>>
{
    private const string AppRequestName = nameof(GetWorkItemsQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkItemsQueryHandler> _logger = logger;

    public async Task<Result<IReadOnlyCollection<WorkItemListDto>>> Handle(GetWorkItemsQuery request, CancellationToken cancellationToken)
    {
        var query = _workDbContext.WorkItems
            .AsQueryable();

        if (request.Id.HasValue)
        {
            query = query.Where(e => e.WorkspaceId == request.Id);
        }
        else if (request.Key is not null)
        {
            query = query.Where(e => e.Workspace.Key == request.Key);
        }
        else
        {
            _logger.LogError("{AppRequestName}: No workspace id or key provided. {@Request}", AppRequestName, request);
            return Result.Failure<IReadOnlyCollection<WorkItemListDto>>("No valid workspace id or key provided.");
        }

        return await query.ProjectToType<WorkItemListDto>().ToListAsync(cancellationToken);
    }
}
