using Wayd.Common.Application.Dtos;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>An open project task assigned to the caller, with the project it belongs to.</summary>
public sealed record MyProjectTaskDto
{
    /// <summary>The task's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The task's key, such as GEMINI-12: its project's key and its number in the project.</summary>
    public required string Key { get; init; }

    /// <summary>The task's name.</summary>
    public required string Name { get; init; }

    /// <summary>Task or Milestone.</summary>
    public required SimpleNavigationDto Type { get; init; }

    /// <summary>Not Started or In Progress: the list holds open tasks only.</summary>
    public required SimpleNavigationDto Status { get; init; }

    /// <summary>How urgent the task is: Low, Medium, High or Critical.</summary>
    public required SimpleNavigationDto Priority { get; init; }

    /// <summary>
    /// When the task is due: a task's planned end, or a milestone's planned date. Null when none is planned.
    /// </summary>
    public LocalDate? DueDate { get; init; }

    /// <summary>The project the task belongs to.</summary>
    public required ProjectNavigationDto Project { get; init; }
}
