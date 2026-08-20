using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a stage definition within a project lifecycle template.
/// </summary>
public sealed class ProjectLifecycleStage : BaseAuditableEntity
{
    private ProjectLifecycleStage() { }

    internal ProjectLifecycleStage(Guid projectLifecycleId, string name, string description, int order)
    {
        ProjectLifecycleId = projectLifecycleId;
        Name = name;
        Description = description;
        Order = order;
    }

    /// <summary>
    /// The ID of the lifecycle this stage belongs to.
    /// </summary>
    public Guid ProjectLifecycleId { get; private init; }

    /// <summary>
    /// The name of the stage (e.g., "Planning", "Execution", "Closure").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A description of the stage's purpose and expected activities.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The display order of the stage within the lifecycle.
    /// </summary>
    public int Order { get; internal set; }

    /// <summary>
    /// Updates the stage details.
    /// </summary>
    internal Result Update(string name, string description)
    {
        Name = name;
        Description = description;
        return Result.Success();
    }
}
