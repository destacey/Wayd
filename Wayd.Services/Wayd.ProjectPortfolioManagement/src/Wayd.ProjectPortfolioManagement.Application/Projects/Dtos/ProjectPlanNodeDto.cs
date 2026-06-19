using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// Unified DTO for the project plan tree that includes both phases and tasks as nodes.
/// </summary>
public sealed record ProjectPlanNodeDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// The type of node: "Phase" or "Task".
    /// </summary>
    public required string NodeType { get; set; }

    public required string Name { get; set; }
    public required SimpleNavigationDto Status { get; set; }
    public int Order { get; set; }

    /// <summary>
    /// The Work Breakdown Structure code (e.g., "1" for phases, "1.1.2" for tasks).
    /// </summary>
    public required string Wbs { get; set; }

    public LocalDate? Start { get; set; }
    public LocalDate? End { get; set; }
    public decimal Progress { get; set; }
    public List<EmployeeNavigationDto> Assignees { get; set; } = [];

    /// <summary>
    /// The child nodes (tasks under a phase, or sub-tasks under a task).
    /// </summary>
    public List<ProjectPlanNodeDto> Children { get; set; } = [];

    // ---- Task-only fields (null/empty for phase nodes) ----

    /// <summary>
    /// The task key (e.g., "PROJ-123"). Null for phase nodes.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// The task type (Task or Milestone). Null for phase nodes.
    /// </summary>
    public SimpleNavigationDto? Type { get; set; }

    /// <summary>
    /// The task priority. Null for phase nodes.
    /// </summary>
    public SimpleNavigationDto? Priority { get; set; }

    /// <summary>
    /// The parent task ID. Null for root tasks and phase nodes.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// The phase ID this task belongs to. Null for phase nodes.
    /// </summary>
    public Guid? ProjectPhaseId { get; set; }

    /// <summary>
    /// The milestone planned date. Null for non-milestone tasks and phase nodes.
    /// </summary>
    public LocalDate? PlannedDate { get; set; }

    /// <summary>
    /// The estimated effort in hours. Null for phase nodes.
    /// </summary>
    public decimal? EstimatedEffortHours { get; set; }
}
