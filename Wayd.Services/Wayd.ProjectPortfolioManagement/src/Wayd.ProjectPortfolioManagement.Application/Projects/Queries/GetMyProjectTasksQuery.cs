using Wayd.Common.Application.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

/// <summary>
/// The open tasks and milestones assigned to the caller's linked employee, across every project, soonest due
/// first: past due before due today and later, those with no date last. Empty when the account is not linked.
/// <paramref name="StatusFilter"/> picks the projects by status; left out, it is <see cref="DefaultStatuses"/>.
/// </summary>
public sealed record GetMyProjectTasksQuery(ProjectStatus[]? StatusFilter = null) : IQuery<IReadOnlyList<MyProjectTaskDto>>
{
    /// <summary>
    /// The projects whose tasks are live work. Completing or canceling a project leaves its open tasks open, so
    /// without this a closed project's tasks would read as overdue forever.
    /// </summary>
    public static readonly ProjectStatus[] DefaultStatuses = [ProjectStatus.Approved, ProjectStatus.Active];
}

/// <summary>Reads the caller's open tasks for <see cref="GetMyProjectTasksQuery"/>.</summary>
public sealed class GetMyProjectTasksQueryHandler(
    IProjectPortfolioManagementDbContext ppmDbContext,
    ICurrentPrincipal currentPrincipal)
    : IQueryHandler<GetMyProjectTasksQuery, IReadOnlyList<MyProjectTaskDto>>
{
    private static readonly TaskStatus[] OpenStatuses = [TaskStatus.NotStarted, TaskStatus.InProgress];

    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    public async Task<IReadOnlyList<MyProjectTaskDto>> Handle(GetMyProjectTasksQuery request, CancellationToken cancellationToken)
    {
        // Resolved rather than read from the token claim, so an account linked mid-session sees its tasks.
        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (!employeeId.HasValue)
            return [];

        var eid = employeeId.Value;
        var tasks = _ppmDbContext.ProjectTasks
            .Where(t => OpenStatuses.Contains(t.Status))
            .Where(t => t.Roles.Any(r => r.EmployeeId == eid && r.Role == TaskRole.Assignee));

        var statuses = request.StatusFilter is { Length: > 0 } ? request.StatusFilter : GetMyProjectTasksQuery.DefaultStatuses;
        tasks = tasks.Where(t => statuses.Contains(t.Project.Status));

        var rows = await tasks
            .Select(t => new
            {
                t.Id,
                t.Key,
                t.Number,
                t.Name,
                t.Type,
                t.Status,
                t.Priority,
                End = t.PlannedDateRange != null ? t.PlannedDateRange.End : null,
                t.PlannedDate,
                ProjectId = t.Project.Id,
                ProjectKey = t.Project.Key,
                ProjectName = t.Project.Name,
            })
            .ToListAsync(cancellationToken);

        return [.. rows
            .Select(t => new
            {
                t.Number,
                ProjectKey = t.ProjectKey.Value,
                Dto = new MyProjectTaskDto
                {
                    Id = t.Id,
                    Key = t.Key.Value,
                    Name = t.Name,
                    Type = SimpleNavigationDto.FromEnum(t.Type),
                    Status = SimpleNavigationDto.FromEnum(t.Status),
                    Priority = SimpleNavigationDto.FromEnum(t.Priority),
                    DueDate = t.Type == ProjectTaskType.Milestone ? t.PlannedDate : t.End,
                    Project = new ProjectNavigationDto { Id = t.ProjectId, Key = t.ProjectKey.Value, Name = t.ProjectName },
                },
            })
            .OrderBy(t => t.Dto.DueDate is null)
            .ThenBy(t => t.Dto.DueDate)
            .ThenBy(t => t.Dto.Project.Name, StringComparer.OrdinalIgnoreCase)
            // Project key, then task number, last: unique, so an MCP caller's offset slices the same list on every
            // call, and numeric, so task 2 comes before task 10.
            .ThenBy(t => t.ProjectKey, StringComparer.Ordinal)
            .ThenBy(t => t.Number)
            .Select(t => t.Dto)];
    }
}
