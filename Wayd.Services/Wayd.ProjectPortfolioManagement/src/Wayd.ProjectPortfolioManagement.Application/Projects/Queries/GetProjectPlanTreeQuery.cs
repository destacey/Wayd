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
            .Include(p => p.Stages)
                .ThenInclude(p => p.Roles)
                    .ThenInclude(r => r.Employee)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.Roles)
                    .ThenInclude(r => r.Employee)
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
            return [];

        var stages = project.Stages.OrderBy(p => p.Order).ToList();
        var tasks = project.Tasks.ToList();

        if (stages.Count == 0)
            return [];

        // Calculate WBS codes for all tasks with stage prefix
        var wbsCodes = WbsCalculator.CalculateAllWbs(tasks, stages);

        // Build stage nodes with task children
        var stageNodes = new List<ProjectPlanNodeDto>();
        foreach (var stage in stages)
        {
            var stageNode = MapStageToNode(stage);

            // Get root tasks for this stage
            var rootTasks = tasks
                .Where(t => t.ProjectStageId == stage.Id && t.ParentId is null)
                .OrderBy(t => t.Order)
                .ToList();

            stageNode.Children = [.. rootTasks.Select(t => MapTaskToNode(t, tasks, wbsCodes))];

            stageNodes.Add(stageNode);
        }

        return stageNodes;
    }

    private static ProjectPlanNodeDto MapStageToNode(ProjectStage stage)
    {
        return new ProjectPlanNodeDto
        {
            Id = stage.Id,
            NodeType = "Stage",
            Name = stage.Name,
            Status = SimpleNavigationDto.FromEnum(stage.Status),
            Order = stage.Order,
            Wbs = stage.Order.ToString(),
            Start = stage.DateRange?.Start,
            End = stage.DateRange?.End,
            Progress = stage.Progress.Value,
            Assignees = [.. stage.Roles
                .Where(r => r.Role == ProjectStageRole.Assignee)
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
            ProjectStageId = task.ProjectStageId,
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
