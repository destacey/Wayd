namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// Aggregated task metrics across the projects an employee is involved in.
/// </summary>
public sealed record ProjectsTaskMetricsDto
{
    /// <summary>
    /// Open tasks past their planned end date.
    /// </summary>
    public int Overdue { get; init; }

    /// <summary>
    /// Open tasks due by end of this week (Saturday).
    /// </summary>
    public int DueThisWeek { get; init; }

    /// <summary>
    /// Open tasks due next week (Sunday through Saturday).
    /// </summary>
    public int Upcoming { get; init; }
}
