namespace Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Models;

/// <summary>
/// Represents an object's ID and task key (e.g., "APOLLO-1").
/// </summary>
public sealed record ProjectTaskIdAndKey(Guid Id, string Key);
