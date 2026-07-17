using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;

/// <summary>
/// Gets the critical path for a project (ordered list of task IDs on the critical path).
/// Note: This is a stub implementation. Full CPM (Critical Path Method) algorithm to be implemented later.
/// </summary>
public sealed record GetCriticalPathQuery(string ProjectIdOrKey) : IQuery<IReadOnlyList<Guid>>;

public sealed class GetCriticalPathQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ILogger<GetCriticalPathQueryHandler> logger)
    : IQueryHandler<GetCriticalPathQuery, IReadOnlyList<Guid>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ILogger<GetCriticalPathQueryHandler> _logger = logger;

    public async Task<IReadOnlyList<Guid>> Handle(GetCriticalPathQuery request, CancellationToken cancellationToken)
    {
        // Resolve project ID from IdOrKey
        var projectId = await ResolveProjectId(request.ProjectIdOrKey, cancellationToken);
        if (projectId is null)
        {
            _logger.LogWarning("GetCriticalPath: Project with identifier '{ProjectIdOrKey}' not found.", request.ProjectIdOrKey);
            return Array.Empty<Guid>();
        }

        // TODO: Implement full Critical Path Method (CPM) algorithm
        // For now, return an empty list as a stub implementation
        //
        // Full implementation should:
        // 1. Load all tasks with their dependencies and date ranges
        // 2. Calculate Early Start (ES) and Early Finish (EF) for each task (forward pass)
        // 3. Calculate Late Start (LS) and Late Finish (LF) for each task (backward pass)
        // 4. Calculate slack/float for each task (LS - ES or LF - EF)
        // 5. Identify critical tasks (tasks with zero slack)
        // 6. Return the ordered list of critical task IDs

        _logger.LogInformation("GetCriticalPath: Critical path calculation requested for project {ProjectId}. Returning empty stub result.", projectId);

        return Array.Empty<Guid>();
    }

    private async Task<Guid?> ResolveProjectId(string projectIdOrKey, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(projectIdOrKey, out var id))
        {
            return id;
        }

        try
        {
            var key = new ProjectKey(projectIdOrKey);
            var projectId = await _ppmDbContext.Projects
                .Where(p => p.Key == key)
                .Select(p => p.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return projectId;
        }
        catch
        {
            return null;
        }
    }
}
