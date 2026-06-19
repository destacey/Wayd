using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;

/// <summary>
/// Lightweight DTO for a project task in list views.
/// </summary>
public sealed record ProjectTaskListDto : IMapFrom<ProjectTask>
{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public Guid ProjectId { get; set; }
    public required string Name { get; set; }
    public required SimpleNavigationDto Type { get; set; }
    public required SimpleNavigationDto Status { get; set; }
    public required SimpleNavigationDto Priority { get; set; }

    /// <summary>
    /// The assignees of the project task.
    /// </summary>
    public required List<EmployeeNavigationDto> Assignees { get; set; } = [];

    /// <summary>
    /// The progress of the task as a percentage (0 to 100).
    /// </summary>
    public decimal Progress { get; set; }
    public int Order { get; set; }
    public Guid? ParentId { get; set; }
    public ProjectTaskNavigationDto? Parent { get; set; }
    public LocalDate? PlannedStart { get; set; }
    public LocalDate? PlannedEnd { get; set; }
    public LocalDate? PlannedDate { get; set; }
    public decimal? EstimatedEffortHours { get; set; }

    public void ConfigureMapping(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectTask, ProjectTaskListDto>()
            .Map(dest => dest.Key, src => src.Key.Value)
            .Map(dest => dest.Type, src => SimpleNavigationDto.FromEnum(src.Type))
            .Map(dest => dest.Status, src => SimpleNavigationDto.FromEnum(src.Status))
            .Map(dest => dest.Progress, src => src.Progress.Value)
            .Map(dest => dest.Assignees, src => src.Roles.Where(r => r.Role == TaskRole.Assignee).Select(r => EmployeeNavigationDto.From(r.Employee!)).ToList())
            .Map(dest => dest.Priority, src => SimpleNavigationDto.FromEnum(src.Priority))
            .Map(dest => dest.PlannedStart, src => src.PlannedDateRange != null ? src.PlannedDateRange.Start : (LocalDate?)null)
            .Map(dest => dest.PlannedEnd, src => src.PlannedDateRange != null ? src.PlannedDateRange.End : null);
    }
}
