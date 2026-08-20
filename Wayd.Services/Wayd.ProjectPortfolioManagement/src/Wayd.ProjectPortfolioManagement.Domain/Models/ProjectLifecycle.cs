using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a project lifecycle template that defines the ordered stages a project goes through.
/// Lifecycles enforce consistency across projects by standardizing the top-level planning structure.
/// </summary>
public sealed class ProjectLifecycle : BaseAuditableEntity, IHasIdAndKey
{
    private readonly List<ProjectLifecycleStage> _stages = [];

    private ProjectLifecycle() { }

    private ProjectLifecycle(string name, string description)
    {
        Name = name;
        Description = description;
        State = ProjectLifecycleState.Proposed;
    }

    /// <summary>
    /// The unique auto-generated key of the lifecycle. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The name of the lifecycle (e.g., "Standard Waterfall", "Product/Software Delivery").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// A description of the lifecycle's purpose and recommended use cases.
    /// </summary>
    public string Description
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Description)).Trim();
    } = default!;

    /// <summary>
    /// The current state of the lifecycle (Proposed, Active, Archived).
    /// </summary>
    public ProjectLifecycleState State { get; private set; }

    /// <summary>
    /// The ordered stages defined in this lifecycle template.
    /// </summary>
    public IReadOnlyCollection<ProjectLifecycleStage> Stages => _stages.AsReadOnly();

    /// <summary>
    /// Indicates whether the lifecycle can be deleted. Only proposed lifecycles can be deleted.
    /// </summary>
    public bool CanBeDeleted() => State is ProjectLifecycleState.Proposed;

    /// <summary>
    /// Updates the lifecycle details. Only allowed when the lifecycle is in the Proposed state.
    /// </summary>
    public Result Update(string name, string description)
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Only proposed lifecycles can be updated.");
        }

        Name = name;
        Description = description;

        return Result.Success();
    }

    #region Lifecycle State Transitions

    /// <summary>
    /// Activates the lifecycle, making it available for project assignment.
    /// Requires at least one stage to be defined.
    /// </summary>
    public Result Activate()
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Only proposed lifecycles can be activated.");
        }

        if (_stages.Count == 0)
        {
            return Result.Failure("A lifecycle must have at least one stage before it can be activated.");
        }

        State = ProjectLifecycleState.Active;

        return Result.Success();
    }

    /// <summary>
    /// Archives the lifecycle, preventing it from being assigned to new projects.
    /// Existing projects using this lifecycle are not affected.
    /// </summary>
    public Result Archive()
    {
        if (State != ProjectLifecycleState.Active)
        {
            return Result.Failure("Only active lifecycles can be archived.");
        }

        State = ProjectLifecycleState.Archived;

        return Result.Success();
    }

    #endregion Lifecycle State Transitions

    #region Stage Management

    /// <summary>
    /// Adds a new stage to the lifecycle. Only allowed when the lifecycle is in the Proposed state.
    /// The stage is appended at the end of the existing stages.
    /// </summary>
    public Result<ProjectLifecycleStage> AddStage(string name, string description)
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure<ProjectLifecycleStage>("Stages can only be added to proposed lifecycles.");
        }

        var order = _stages.Count > 0 ? _stages.Max(p => p.Order) + 1 : 1;

        var stage = new ProjectLifecycleStage(Id, name, description, order);
        _stages.Add(stage);

        return Result.Success(stage);
    }

    /// <summary>
    /// Updates the details of an existing stage. Only allowed when the lifecycle is in the Proposed state.
    /// </summary>
    public Result UpdateStage(Guid stageId, string name, string description)
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Stages can only be updated on proposed lifecycles.");
        }

        var stage = _stages.FirstOrDefault(p => p.Id == stageId);
        if (stage is null)
        {
            return Result.Failure("Stage not found.");
        }

        return stage.Update(name, description);
    }

    /// <summary>
    /// Removes a stage from the lifecycle and reorders remaining stages.
    /// Only allowed when the lifecycle is in the Proposed state.
    /// </summary>
    public Result RemoveStage(Guid stageId)
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Stages can only be removed from proposed lifecycles.");
        }

        var stage = _stages.FirstOrDefault(p => p.Id == stageId);
        if (stage is null)
        {
            return Result.Failure("Stage not found.");
        }

        _stages.Remove(stage);

        ReorderStages();

        return Result.Success();
    }

    /// <summary>
    /// Reorders the stages based on the provided ordered list of stage IDs.
    /// Only allowed when the lifecycle is in the Proposed state.
    /// </summary>
    /// <param name="orderedStageIds">The stage IDs in the desired order.</param>
    public Result ReorderStages(List<Guid> orderedStageIds)
    {
        Guard.Against.Null(orderedStageIds, nameof(orderedStageIds));

        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Stages can only be reordered on proposed lifecycles.");
        }

        if (orderedStageIds.Count != _stages.Count)
        {
            return Result.Failure("The number of stage IDs must match the number of existing stages.");
        }

        if (orderedStageIds.Distinct().Count() != orderedStageIds.Count)
        {
            return Result.Failure("Duplicate stage IDs are not allowed.");
        }

        for (int i = 0; i < orderedStageIds.Count; i++)
        {
            var stage = _stages.FirstOrDefault(p => p.Id == orderedStageIds[i]);
            if (stage is null)
            {
                return Result.Failure($"Stage with ID '{orderedStageIds[i]}' not found.");
            }

            stage.Order = i + 1;
        }

        return Result.Success();
    }

    /// <summary>
    /// Resets stage ordering to eliminate gaps after removal.
    /// </summary>
    private void ReorderStages()
    {
        int order = 1;
        foreach (var stage in _stages.OrderBy(p => p.Order))
        {
            stage.Order = order;
            order++;
        }
    }

    #endregion Stage Management

    /// <summary>
    /// Creates a new project lifecycle in the Proposed state.
    /// </summary>
    /// <param name="name">The name of the lifecycle.</param>
    /// <param name="description">A description of the lifecycle's purpose and use cases.</param>
    /// <param name="stages">Optional initial stages to include. Each tuple contains (name, description).</param>
    /// <returns>A new ProjectLifecycle instance.</returns>
    public static ProjectLifecycle Create(string name, string description, IEnumerable<(string Name, string Description)>? stages = null)
    {
        var lifecycle = new ProjectLifecycle(name, description);

        if (stages is not null)
        {
            int order = 1;
            foreach (var (stageName, stageDescription) in stages)
            {
                lifecycle._stages.Add(new ProjectLifecycleStage(lifecycle.Id, stageName, stageDescription, order));
                order++;
            }
        }

        return lifecycle;
    }
}
