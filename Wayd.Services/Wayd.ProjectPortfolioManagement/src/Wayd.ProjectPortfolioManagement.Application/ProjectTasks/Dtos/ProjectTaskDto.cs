using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

/// <summary>
/// Detailed DTO for a project task with full information including assignments.
/// </summary>
public sealed record ProjectTaskDto : IMapFrom<ProjectTask>
{
    /// <summary>
    /// The unique identifier of the task.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The task key in the format {ProjectCode}-{Number} (e.g., "APOLLO-1").
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The unique identifier of the parent project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The name of the task.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// A detailed description of the task.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The task type (Task or Milestone).
    /// </summary>
    public required SimpleNavigationDto Type { get; set; }

    /// <summary>
    /// The current status of the task.
    /// </summary>
    public required SimpleNavigationDto Status { get; set; }

    /// <summary>
    /// The priority of the task.
    /// </summary>
    public required SimpleNavigationDto Priority { get; set; }

    /// <summary>
    /// The assignees of the project task.
    /// </summary>
    public required List<EmployeeNavigationDto> Assignees { get; set; } = [];

    /// <summary>
    /// The progress of the task as a percentage (0 to 100).
    /// </summary>
    public decimal Progress { get; set; }

    /// <summary>
    /// The order of the task within its parent.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The unique identifier of the parent task (null for root tasks).
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// The unique identifier of the project phase this task belongs to.
    /// </summary>
    public Guid ProjectPhaseId { get; set; }

    /// <summary>
    /// The parent task navigation information.
    /// </summary>
    public ProjectTaskNavigationDto? Parent { get; set; }

    /// <summary>
    /// The planned start date for regular tasks.
    /// </summary>
    public LocalDate? PlannedStart { get; set; }

    /// <summary>
    /// The planned end date for regular tasks.
    /// </summary>
    public LocalDate? PlannedEnd { get; set; }

    /// <summary>
    /// The planned date for milestones.
    /// </summary>
    public LocalDate? PlannedDate { get; set; }

    /// <summary>
    /// The estimated effort in hours.
    /// </summary>
    public decimal? EstimatedEffortHours { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectTask, ProjectTaskDto>()
            .Map(dest => dest.Key, src => src.Key.Value)
            .Map(dest => dest.Type, src => SimpleNavigationDto.FromEnum(src.Type))
            .Map(dest => dest.Status, src => SimpleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.Priority, src => SimpleNavigationDto.FromEnum(src.Priority))
            .Map(dest => dest.Assignees, src => src.Roles.Where(r => r.Role == TaskRole.Assignee).Select(x => EmployeeNavigationDto.From(x.Employee!)).ToList())
            .Map(dest => dest.Progress, src => src.Progress.Value)
            .Map(dest => dest.PlannedStart, src => src.PlannedDateRange != null ? src.PlannedDateRange.Start : (LocalDate?)null)
            .Map(dest => dest.PlannedEnd, src => src.PlannedDateRange != null ? src.PlannedDateRange.End : null);
    }
}
