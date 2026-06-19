namespace Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;

/// <summary>
/// Summary metrics for a project's plan, computed from leaf tasks.
/// </summary>
public sealed record ProjectPlanSummaryDto
{
    /// <summary>
    /// Tasks past their end date that are Not Started or In Progress.
    /// </summary>
    public int Overdue { get; init; }

    /// <summary>
    /// Tasks due by end of this week (Saturday) that are Not Started or In Progress.
    /// </summary>
    public int DueThisWeek { get; init; }

    /// <summary>
    /// Tasks due next week (Sunday through Saturday) that are Not Started or In Progress.
    /// </summary>
    public int Upcoming { get; init; }

    /// <summary>
    /// Total number of leaf tasks in the project plan.
    /// </summary>
    public int TotalLeafTasks { get; init; }
}
