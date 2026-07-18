using System.Linq.Expressions;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Application.WorkItemDependencies.Dtos;
using Wayd.Work.Application.Workspaces.Models;

namespace Wayd.Work.Application.WorkItems.Queries;

public sealed record GetWorkItemDependenciesQuery : IQuery<Result<List<ScopedDependencyDto>?>>
{
    public GetWorkItemDependenciesQuery(WorkspaceIdOrKey workspaceIdOrKey, WorkItemKey workItemKey)
    {
        WorkspaceIdOrKeyFilter = workspaceIdOrKey.CreateWorkspaceFilter<WorkItem>();
        WorkItemKey = workItemKey;
    }

    public Expression<Func<WorkItem, bool>> WorkspaceIdOrKeyFilter { get; }
    public WorkItemKey WorkItemKey { get; }
}

public sealed class GetWorkItemDependenciesQueryHandler(IWorkDbContext workDbContext, ILogger<GetWorkItemDependenciesQueryHandler> logger) : IQueryHandler<GetWorkItemDependenciesQuery, Result<List<ScopedDependencyDto>?>>
{
    private const string AppRequestName = nameof(GetWorkItemDependenciesQuery);

    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly ILogger<GetWorkItemDependenciesQueryHandler> _logger = logger;

    public async Task<Result<List<ScopedDependencyDto>?>> Handle(GetWorkItemDependenciesQuery request, CancellationToken cancellationToken)
    {
        var workItemId = await _workDbContext.WorkItems
            .Where(w => w.Key == request.WorkItemKey)
            .Where(request.WorkspaceIdOrKeyFilter)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!workItemId.HasValue)
        {
            _logger.LogInformation("{AppRequestName} - Work item with key {WorkItemKey} not found", AppRequestName, request.WorkItemKey);
            return null;
        }

        var dependencyLinks = await _workDbContext.WorkItemDependencies
            .Where(l => l.RemovedOn == null)
            .Where(l => l.SourceId == workItemId || l.TargetId == workItemId)
            .ProjectToType<DependencyDto>()
            .ToListAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("{AppRequestName} - Found {DependencyLinksCount} dependency links for work item {WorkItemKey}", AppRequestName, dependencyLinks.Count, request.WorkItemKey);

        return dependencyLinks.Count == 0
            ? []
            : dependencyLinks
                .Select(d => ScopedDependencyDto.From(d, request.WorkItemKey))
                .ToList();
    }
}
