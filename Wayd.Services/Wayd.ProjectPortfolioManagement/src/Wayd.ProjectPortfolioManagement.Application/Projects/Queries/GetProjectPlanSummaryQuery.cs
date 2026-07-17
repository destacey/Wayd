using System.Linq.Expressions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>
/// Returns summary metrics for a project's plan, computed from leaf tasks.
/// Optionally filters by an employee (assignee).
/// </summary>
public sealed record GetProjectPlanSummaryQuery : IQuery<ProjectPlanSummaryDto>
{
    public GetProjectPlanSummaryQuery(ProjectIdOrKey idOrKey, Guid? employeeId = null)
    {
        ProjectIdOrKeyFilter = idOrKey.CreateProjectFilter<ProjectTask>();
        EmployeeId = employeeId;
    }

    public Expression<Func<ProjectTask, bool>> ProjectIdOrKeyFilter { get; }
    public Guid? EmployeeId { get; }
}

public sealed class GetProjectPlanSummaryQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetProjectPlanSummaryQuery, ProjectPlanSummaryDto>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<ProjectPlanSummaryDto> Handle(GetProjectPlanSummaryQuery request, CancellationToken cancellationToken)
    {
        var today = _dateTimeProvider.Today;

        // Saturday = ISO day 6. NodaTime uses ISO: Monday=1 ... Sunday=7
        var daysUntilSaturday = ((int)IsoDayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var endOfThisWeek = today.PlusDays(daysUntilSaturday);
        var endOfNextWeek = endOfThisWeek.PlusDays(7);

        // Query leaf tasks: tasks with no children
        var query = _ppmDbContext.ProjectTasks
            .Where(request.ProjectIdOrKeyFilter)
            .Where(t => !_ppmDbContext.ProjectTasks.Any(child => child.ParentId == t.Id));

        if (request.EmployeeId.HasValue)
        {
            var employeeId = request.EmployeeId.Value;
            query = query.Where(t =>
                t.Roles.Any(r => r.Role == TaskRole.Assignee && r.EmployeeId == employeeId));
        }

        // Only count open tasks (Not Started or In Progress) that have an end date
        var openStatuses = new[] { Domain.Enums.TaskStatus.NotStarted, Domain.Enums.TaskStatus.InProgress };

        var totalLeafTasks = await query.CountAsync(cancellationToken);

        var openTasksWithEndDate = query
            .Where(t => openStatuses.Contains(t.Status))
            .Where(t => t.PlannedDateRange != null && t.PlannedDateRange.End != null);

        var overdue = await openTasksWithEndDate
            .CountAsync(t => t.PlannedDateRange!.End < today, cancellationToken);

        var dueThisWeek = await openTasksWithEndDate
            .CountAsync(t => t.PlannedDateRange!.End >= today
                          && t.PlannedDateRange!.End <= endOfThisWeek, cancellationToken);

        var upcoming = await openTasksWithEndDate
            .CountAsync(t => t.PlannedDateRange!.End > endOfThisWeek
                          && t.PlannedDateRange!.End <= endOfNextWeek, cancellationToken);

        return new ProjectPlanSummaryDto
        {
            Overdue = overdue,
            DueThisWeek = dueThisWeek,
            Upcoming = upcoming,
            TotalLeafTasks = totalLeafTasks,
        };
    }
}
