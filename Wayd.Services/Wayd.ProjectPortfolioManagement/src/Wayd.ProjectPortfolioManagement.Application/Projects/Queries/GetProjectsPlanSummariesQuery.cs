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
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetProjectsPlanSummariesQuery, Dictionary<Guid, ProjectPlanSummaryDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Dictionary<Guid, ProjectPlanSummaryDto>> Handle(
        GetProjectsPlanSummariesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProjectIds.Length == 0)
        {
            return [];
        }

        // Resolved rather than read from the token claim, which is a snapshot taken at sign-in: a user
        // linked mid-session would otherwise see nothing until they signed in again. Empty remains the
        // honest answer for a genuinely unlinked account.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
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

        // All tasks across requested projects
        var allTasks = _ppmDbContext.ProjectTasks
            .Where(t => request.ProjectIds.Contains(t.ProjectId));

        // Apply role-based visibility
        IQueryable<Domain.Models.ProjectTask> visibleTasks;

        if (activeLeadershipRoles.Length > 0)
        {
            var leadershipTasks = allTasks
                .Where(t => t.Project.Roles.Any(r => r.EmployeeId == eid && activeLeadershipRoles.Contains(r.Role)));

            var assigneeTasks = allTasks
                .Where(t => !t.Project.Roles.Any(r => r.EmployeeId == eid && activeLeadershipRoles.Contains(r.Role)))
                .Where(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee));

            visibleTasks = leadershipTasks.Concat(assigneeTasks);
        }
        else
        {
            visibleTasks = allTasks
                .Where(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee));
        }

        // The total is leaf-only: it gates whether a summary renders at all,
        // and counting a parent alongside its children would double-count.
        var visibleLeafTasks = visibleTasks
            .Where(t => !_ppmDbContext.ProjectTasks.Any(child => child.ParentId == t.Id));

        // The date metrics count every dated task, parents included, so they
        // agree with the plan grid's Schedule column.
        var openVisibleTasks = visibleTasks
            .Where(t => openStatuses.Contains(t.Status))
            .Where(t => t.PlannedDateRange != null && t.PlannedDateRange.End != null);

        // EF Core cannot translate GroupBy with conditional counts on owned type
        // properties (PlannedDateRange.End). Materialize the minimal projection
        // (ProjectId + EndDate) and aggregate in memory. The data set is small —
        // only open tasks with end dates across the user's visible projects.
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
