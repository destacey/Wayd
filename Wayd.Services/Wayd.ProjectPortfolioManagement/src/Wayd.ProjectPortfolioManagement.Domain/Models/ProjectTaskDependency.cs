using Wayd.ProjectPortfolioManagement.Domain.Enums;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a dependency relationship between two project tasks.
/// </summary>
public sealed class ProjectTaskDependency : BaseAuditableEntity
{
    private ProjectTaskDependency() { }

    private ProjectTaskDependency(
        Guid predecessorId,
        Guid successorId,
        DependencyType type)
    {
        PredecessorId = predecessorId;
        SuccessorId = successorId;
        Type = type;
    }

    /// <summary>
    /// The ID of the predecessor task (the task that must be completed first).
    /// </summary>
    public Guid PredecessorId { get; private init; }

    /// <summary>
    /// The predecessor task.
    /// </summary>
    public ProjectTask? Predecessor { get; private set; }

    /// <summary>
    /// The ID of the successor task (the task that depends on the predecessor).
    /// </summary>
    public Guid SuccessorId { get; private init; }

    /// <summary>
    /// The successor task.
    /// </summary>
    public ProjectTask? Successor { get; private set; }

    /// <summary>
    /// The type of dependency relationship.
    /// </summary>
    public DependencyType Type { get; private set; }

    /// <summary>
    /// The timestamp when this dependency was removed (soft delete).
    /// </summary>
    public Instant? RemovedOn { get; private set; }

    /// <summary>
    /// The ID of the user who removed this dependency.
    /// </summary>
    public Guid? RemovedById { get; private set; }

    /// <summary>
    /// Indicates whether this dependency is currently active.
    /// </summary>
    public bool IsActive => RemovedOn is null;

    /// <summary>
    /// Removes (soft deletes) this dependency.
    /// </summary>
    internal void Remove(Instant timestamp)
    {
        RemovedOn = timestamp;
        // TODO: Set RemovedById from current user context when available
    }

    /// <summary>
    /// Creates a new dependency between two tasks.
    /// </summary>
    internal static ProjectTaskDependency Create(Guid predecessorId, Guid successorId, DependencyType type)
    {
        return new ProjectTaskDependency(predecessorId, successorId, type);
    }
}
