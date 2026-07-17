using System.Linq.Expressions;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Extensions;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectTeamQuery : IQuery<List<ProjectTeamMemberDto>?>
{
    public GetProjectTeamQuery(ProjectIdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Project>();
    }

    public Expression<Func<Project, bool>> IdOrKeyFilter { get; }
}

public sealed class GetProjectTeamQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectTeamQuery, List<ProjectTeamMemberDto>?>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<List<ProjectTeamMemberDto>?> Handle(GetProjectTeamQuery request, CancellationToken cancellationToken)
    {
        var project = await _ppmDbContext.Projects
            .Where(request.IdOrKeyFilter)
            .Select(p => new
            {
                p.Id,
                ProjectRoles = p.Roles.Select(r => new
                {
                    r.EmployeeId,
                    r.Role,
                    EmployeeKey = r.Employee!.Key,
                    EmployeeName = r.Employee.Name.FirstName + " " + r.Employee.Name.LastName,
                }).ToList(),
                PhaseAssignments = p.Phases
                    .OrderBy(ph => ph.Order)
                    .SelectMany(ph => ph.Roles.Select(r => new
                    {
                        r.EmployeeId,
                        PhaseName = ph.Name,
                    }))
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
            return null;

        // Get active leaf task counts and assignee info per employee
        var activeStatuses = new[] { TaskStatus.NotStarted, TaskStatus.InProgress };
        var projectId = project.Id;

        var taskAssigneeData = await _ppmDbContext.ProjectTasks
            .Where(t => t.ProjectId == projectId)
            .Where(t => !_ppmDbContext.ProjectTasks.Any(child => child.ParentId == t.Id)) // leaf tasks only
            .Where(t => activeStatuses.Contains(t.Status))
            .SelectMany(t => t.Roles
                .Where(r => r.Role == TaskRole.Assignee)
                .Select(r => new
                {
                    r.EmployeeId,
                    EmployeeKey = r.Employee!.Key,
                    EmployeeName = r.Employee.Name.FirstName + " " + r.Employee.Name.LastName,
                }))
            .ToListAsync(cancellationToken);

        var activeTaskCounts = taskAssigneeData
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Build phase assignments lookup
        var phasesByEmployee = project.PhaseAssignments
            .GroupBy(pa => pa.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(pa => pa.PhaseName).Distinct().ToList());

        // Collect all unique employees from project roles and task assignments
        var employeeMap = new Dictionary<Guid, (int Key, string Name, List<string> Roles)>();

        foreach (var role in project.ProjectRoles)
        {
            if (!employeeMap.TryGetValue(role.EmployeeId, out var entry))
            {
                entry = (role.EmployeeKey, role.EmployeeName, []);
                employeeMap[role.EmployeeId] = entry;
            }
            entry.Roles.Add(role.Role.GetDisplayName());
        }

        // Add task-only assignees (people assigned to tasks but not in a project role)
        foreach (var group in taskAssigneeData.GroupBy(x => x.EmployeeId))
        {
            if (!employeeMap.ContainsKey(group.Key))
            {
                var first = group.First();
                employeeMap[group.Key] = (first.EmployeeKey, first.EmployeeName, []);
            }
        }

        // Build team members
        var teamMembers = employeeMap
            .Select(kvp => new ProjectTeamMemberDto
            {
                Employee = new EmployeeNavigationDto
                {
                    Id = kvp.Key,
                    Key = kvp.Value.Key,
                    Name = kvp.Value.Name,
                },
                Roles = kvp.Value.Roles,
                AssignedPhases = phasesByEmployee.TryGetValue(kvp.Key, out var phases) ? phases : [],
                ActiveWorkItemCount = activeTaskCounts.TryGetValue(kvp.Key, out var count) ? count : 0,
            })
            .OrderBy(m => m.Employee.Name)
            .ToList();

        return teamMembers;
    }
}
