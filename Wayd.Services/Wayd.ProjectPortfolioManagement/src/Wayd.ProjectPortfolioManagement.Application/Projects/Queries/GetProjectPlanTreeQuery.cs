using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Services;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Queries;

public sealed record GetProjectPlanTreeQuery : IQuery<IReadOnlyList<ProjectPlanNodeDto>>
{
    public GetProjectPlanTreeQuery(ProjectIdOrKey projectIdOrKey)
    {
        ProjectIdOrKeyFilter = projectIdOrKey.CreateFilter<Project>();
    }

    public Expression<Func<Project, bool>> ProjectIdOrKeyFilter { get; }
}

public sealed class GetProjectPlanTreeQueryHandler(IProjectPortfolioManagementDbContext ppmDbContext)
    : IQueryHandler<GetProjectPlanTreeQuery, IReadOnlyList<ProjectPlanNodeDto>>
{
    private readonly IProjectPortfolioManagementDbContext _ppmDbContext = ppmDbContext;

    public async Task<IReadOnlyList<ProjectPlanNodeDto>> Handle(GetProjectPlanTreeQuery request, CancellationToken cancellationToken)
    {
        // Check if the project has a lifecycle before loading the full plan tree
        var hasLifecycle = await _ppmDbContext.Projects
            .AsNoTracking()
            .Where(request.ProjectIdOrKeyFilter)
            .AnyAsync(p => p.ProjectLifecycleId != null, cancellationToken);

        if (!hasLifecycle)
            return [];

        var project = await _ppmDbContext.Projects
            .AsNoTracking()
            .Where(request.ProjectIdOrKeyFilter)
            .Include(p => p.Phases)
                .ThenInclude(p => p.Roles)
                    .ThenInclude(r => r.Employee)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.Roles)
                    .ThenInclude(r => r.Employee)
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
            return [];

        var phases = project.Phases.OrderBy(p => p.Order).ToList();
        var tasks = project.Tasks.ToList();

        if (phases.Count == 0)
            return [];

        // Calculate WBS codes for all tasks with phase prefix
        var wbsCodes = WbsCalculator.CalculateAllWbs(tasks, phases);

        // Build phase nodes with task children
        var phaseNodes = new List<ProjectPlanNodeDto>();
        foreach (var phase in phases)
        {
            var phaseNode = MapPhaseToNode(phase);

            // Get root tasks for this phase
            var rootTasks = tasks
                .Where(t => t.ProjectPhaseId == phase.Id && t.ParentId is null)
                .OrderBy(t => t.Order)
                .ToList();

            phaseNode.Children = [.. rootTasks.Select(t => MapTaskToNode(t, tasks, wbsCodes))];

            phaseNodes.Add(phaseNode);
        }

        return phaseNodes;
    }

    private static ProjectPlanNodeDto MapPhaseToNode(ProjectPhase phase)
    {
        return new ProjectPlanNodeDto
        {
            Id = phase.Id,
            NodeType = "Phase",
            Name = phase.Name,
            Status = SimpleNavigationDto.FromEnum(phase.Status),
            Order = phase.Order,
            Wbs = phase.Order.ToString(),
            Start = phase.DateRange?.Start,
            End = phase.DateRange?.End,
            Progress = phase.Progress.Value,
            Assignees = [.. phase.Roles
                .Where(r => r.Role == ProjectPhaseRole.Assignee)
                .Select(r => EmployeeNavigationDto.From(r.Employee!))],
        };
    }

    private static ProjectPlanNodeDto MapTaskToNode(ProjectTask task, List<ProjectTask> allTasks, Dictionary<Guid, string> wbsCodes)
    {
        var node = new ProjectPlanNodeDto
        {
            Id = task.Id,
            NodeType = "Task",
            Name = task.Name,
            Status = SimpleNavigationDto.FromEnum(task.Status),
            Order = task.Order,
            Wbs = wbsCodes[task.Id],
            Start = task.PlannedDateRange?.Start,
            End = task.PlannedDateRange?.End,
            Progress = task.Progress.Value,
            Assignees = [.. task.Roles
                .Where(r => r.Role == TaskRole.Assignee)
                .Select(r => EmployeeNavigationDto.From(r.Employee!))],
            Key = task.Key.Value,
            Type = SimpleNavigationDto.FromEnum(task.Type),
            Priority = SimpleNavigationDto.FromEnum(task.Priority),
            ParentId = task.ParentId,
            ProjectPhaseId = task.ProjectPhaseId,
            PlannedDate = task.PlannedDate,
            EstimatedEffortHours = task.EstimatedEffortHours,
        };

        // Recursively build children
        var children = allTasks
            .Where(t => t.ParentId == task.Id)
            .OrderBy(t => t.Order)
            .ToList();

        node.Children = [.. children.Select(t => MapTaskToNode(t, allTasks, wbsCodes))];

        return node;
    }
}
