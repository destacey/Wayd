using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>
/// Returns plan summary metrics for multiple projects in a single query.
/// Applies the same per-project leadership vs. assignee visibility rules
/// as <see cref="GetMyProjectsTaskMetricsQuery"/>: when the user holds a
/// selected leadership role on a project, all tasks are visible; otherwise,
/// only tasks assigned to the user are counted.
/// </summary>
public sealed record GetProjectsPlanSummariesQuery(
    Guid[] ProjectIds,
    ProjectMemberRole[]? RoleFilter = null) : IQuery<Dictionary<Guid, ProjectPlanSummaryDto>>;

public sealed class GetProjectsPlanSummariesQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetProjectsPlanSummariesQuery, Dictionary<Guid, ProjectPlanSummaryDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Dictionary<Guid, ProjectPlanSummaryDto>> Handle(
        GetProjectsPlanSummariesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProjectIds.Length == 0)
        {
            return [];
        }

        var employeeId = _currentUser.GetEmployeeId();
        if (!employeeId.HasValue)
        {
            return [];
        }

        var eid = employeeId.Value;
        var today = _dateTimeProvider.Today;

        var daysUntilSaturday = ((int)IsoDayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var endOfThisWeek = today.PlusDays(daysUntilSaturday);
        var endOfNextWeek = endOfThisWeek.PlusDays(7);

        var openStatuses = new[] { Domain.Enums.TaskStatus.NotStarted, Domain.Enums.TaskStatus.InProgress };
        var allLeadershipRoles = new[] { ProjectRole.Sponsor, ProjectRole.Owner, ProjectRole.Manager };

        var activeLeadershipRoles = request.RoleFilter is { Length: > 0 }
            ? [.. request.RoleFilter
                .Where(r => r != ProjectMemberRole.Assignee && r != ProjectMemberRole.Member)
                .Select(r => (ProjectRole)(int)r)]
            : allLeadershipRoles;

        // All leaf tasks across requested projects
        var leafTasks = _ppmDbContext.ProjectTasks
            .Where(t => request.ProjectIds.Contains(t.ProjectId))
            .Where(t => !_ppmDbContext.ProjectTasks.Any(child => child.ParentId == t.Id));

        // Apply role-based visibility to leaf tasks
        IQueryable<Domain.Models.ProjectTask> visibleLeafTasks;

        if (activeLeadershipRoles.Length > 0)
        {
            var leadershipTasks = leafTasks
                .Where(t => t.Project.Roles.Any(r => r.EmployeeId == eid && activeLeadershipRoles.Contains(r.Role)));

            var assigneeTasks = leafTasks
                .Where(t => !t.Project.Roles.Any(r => r.EmployeeId == eid && activeLeadershipRoles.Contains(r.Role)))
                .Where(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee));

            visibleLeafTasks = leadershipTasks.Concat(assigneeTasks);
        }
        else
        {
            visibleLeafTasks = leafTasks
                .Where(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee));
        }

        // Open visible leaf tasks with a planned end date (for date-based metrics)
        var openVisibleTasks = visibleLeafTasks
            .Where(t => openStatuses.Contains(t.Status))
            .Where(t => t.PlannedDateRange != null && t.PlannedDateRange.End != null);

        // EF Core cannot translate GroupBy with conditional counts on owned type
        // properties (PlannedDateRange.End). Materialize the minimal projection
        // (ProjectId + EndDate) and aggregate in memory. The data set is small —
        // only open leaf tasks with end dates across the user's visible projects.
        var taskData = await openVisibleTasks
            .Select(t => new { t.ProjectId, EndDate = t.PlannedDateRange!.End })
            .ToListAsync(cancellationToken);

        var totalLeafCounts = await visibleLeafTasks
            .GroupBy(t => t.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalLeafMap = totalLeafCounts.ToDictionary(x => x.ProjectId, x => x.Count);

        return taskData
            .GroupBy(t => t.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => new ProjectPlanSummaryDto
                {
                    Overdue = g.Count(t => t.EndDate < today),
                    DueThisWeek = g.Count(t => t.EndDate >= today && t.EndDate <= endOfThisWeek),
                    Upcoming = g.Count(t => t.EndDate > endOfThisWeek && t.EndDate <= endOfNextWeek),
                    TotalLeafTasks = totalLeafMap.GetValueOrDefault(g.Key),
                });
    }
}
