using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>
/// Returns aggregated task metrics across all projects the current user is involved in.
/// Computes overdue, due this week, and upcoming counts from open leaf tasks.
/// </summary>
public sealed record GetMyProjectsTaskMetricsQuery(ProjectStatus[]? StatusFilter = null, ProjectMemberRole[]? RoleFilter = null) : IQuery<MyProjectsTaskMetricsDto>;

internal sealed class GetMyProjectsTaskMetricsQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetMyProjectsTaskMetricsQuery, MyProjectsTaskMetricsDto>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<MyProjectsTaskMetricsDto> Handle(GetMyProjectsTaskMetricsQuery request, CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.GetEmployeeId();
        if (!employeeId.HasValue)
        {
            return new MyProjectsTaskMetricsDto();
        }

        var eid = employeeId.Value;
        var today = _dateTimeProvider.Today;

        // Saturday = ISO day 6. NodaTime uses ISO: Monday=1 ... Sunday=7
        var daysUntilSaturday = ((int)IsoDayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var endOfThisWeek = today.PlusDays(daysUntilSaturday);
        var endOfNextWeek = endOfThisWeek.PlusDays(7);

        var openStatuses = new[] { Domain.Enums.TaskStatus.NotStarted, Domain.Enums.TaskStatus.InProgress };

        // Filter to projects the user is involved in
        IQueryable<Domain.Models.Project> projectQuery = _ppmDbContext.Projects;

        if (request.StatusFilter is { Length: > 0 })
        {
            projectQuery = projectQuery.Where(p => request.StatusFilter.Contains(p.Status));
        }

        if (request.RoleFilter is { Length: > 0 })
        {
            var projectRoles = request.RoleFilter
                .Where(r => r != ProjectMemberRole.Assignee)
                .Select(r => (ProjectRole)(int)r)
                .ToArray();

            var includeTaskAssignees = request.RoleFilter.Contains(ProjectMemberRole.Assignee);

            if (projectRoles.Length > 0 && includeTaskAssignees)
            {
                projectQuery = projectQuery.Where(p =>
                    p.Roles.Any(r => r.EmployeeId == eid && projectRoles.Contains(r.Role))
                    || p.Tasks.Any(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee)));
            }
            else if (projectRoles.Length > 0)
            {
                projectQuery = projectQuery.Where(p =>
                    p.Roles.Any(r => r.EmployeeId == eid && projectRoles.Contains(r.Role)));
            }
            else if (includeTaskAssignees)
            {
                projectQuery = projectQuery.Where(p =>
                    p.Tasks.Any(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee)));
            }
        }
        else
        {
            // No role filter — include all projects user is involved in
            projectQuery = projectQuery.Where(p =>
                p.Roles.Any(r => r.EmployeeId == eid)
                || p.Tasks.Any(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee)));
        }

        var projectIds = projectQuery.Select(p => p.Id);

        var allLeadershipRoles = new[] { ProjectRole.Sponsor, ProjectRole.Owner, ProjectRole.Manager };

        // When a role filter is active, only count leadership visibility for the filtered leadership roles.
        // e.g., if filtered to "Task Assignee" only, no leadership roles apply — all tasks scoped to assignee.
        var activeLeadershipRoles = request.RoleFilter is { Length: > 0 }
            ? request.RoleFilter
                .Where(r => r != ProjectMemberRole.Assignee && r != ProjectMemberRole.Member)
                .Select(r => (ProjectRole)(int)r)
                .ToArray()
            : allLeadershipRoles;

        // Open leaf tasks with a planned end date across all matching projects
        var openLeafTasks = _ppmDbContext.ProjectTasks
            .Where(t => projectIds.Contains(t.ProjectId))
            .Where(t => !_ppmDbContext.ProjectTasks.Any(child => child.ParentId == t.Id))
            .Where(t => openStatuses.Contains(t.Status))
            .Where(t => t.PlannedDateRange != null && t.PlannedDateRange.End != null);

        IQueryable<Domain.Models.ProjectTask> relevantTasks;

        if (activeLeadershipRoles.Length > 0)
        {
            // Leadership projects: all tasks visible
            var leadershipTasks = openLeafTasks
                .Where(t => t.Project.Roles.Any(r => r.EmployeeId == eid && activeLeadershipRoles.Contains(r.Role)));

            // Non-leadership projects: only tasks assigned to the user
            var assigneeTasks = openLeafTasks
                .Where(t => !t.Project.Roles.Any(r => r.EmployeeId == eid && activeLeadershipRoles.Contains(r.Role)))
                .Where(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee));

            // Concat is safe — the two sets are mutually exclusive (Any vs !Any on same predicate)
            relevantTasks = leadershipTasks.Concat(assigneeTasks);
        }
        else
        {
            // No leadership roles in filter — only count tasks assigned to the user
            relevantTasks = openLeafTasks
                .Where(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee));
        }

        var overdue = await relevantTasks
            .CountAsync(t => t.PlannedDateRange!.End < today, cancellationToken);

        var dueThisWeek = await relevantTasks
            .CountAsync(t => t.PlannedDateRange!.End >= today
                          && t.PlannedDateRange!.End <= endOfThisWeek, cancellationToken);

        var upcoming = await relevantTasks
            .CountAsync(t => t.PlannedDateRange!.End > endOfThisWeek
                          && t.PlannedDateRange!.End <= endOfNextWeek, cancellationToken);

        return new MyProjectsTaskMetricsDto
        {
            Overdue = overdue,
            DueThisWeek = dueThisWeek,
            Upcoming = upcoming,
        };
    }
}
