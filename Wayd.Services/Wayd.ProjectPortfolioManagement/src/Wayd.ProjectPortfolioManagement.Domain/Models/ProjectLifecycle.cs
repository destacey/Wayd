using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Represents a project lifecycle template that defines the ordered phases a project goes through.
/// Lifecycles enforce consistency across projects by standardizing the top-level planning structure.
/// </summary>
public sealed class ProjectLifecycle : BaseAuditableEntity, IHasIdAndKey
{
    private readonly List<ProjectLifecyclePhase> _phases = [];

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
    /// The ordered phases defined in this lifecycle template.
    /// </summary>
    public IReadOnlyCollection<ProjectLifecyclePhase> Phases => _phases.AsReadOnly();

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
    /// Requires at least one phase to be defined.
    /// </summary>
    public Result Activate()
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Only proposed lifecycles can be activated.");
        }

        if (_phases.Count == 0)
        {
            return Result.Failure("A lifecycle must have at least one phase before it can be activated.");
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

    #region Phase Management

    /// <summary>
    /// Adds a new phase to the lifecycle. Only allowed when the lifecycle is in the Proposed state.
    /// The phase is appended at the end of the existing phases.
    /// </summary>
    public Result<ProjectLifecyclePhase> AddPhase(string name, string description)
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure<ProjectLifecyclePhase>("Phases can only be added to proposed lifecycles.");
        }

        var order = _phases.Count > 0 ? _phases.Max(p => p.Order) + 1 : 1;

        var phase = new ProjectLifecyclePhase(Id, name, description, order);
        _phases.Add(phase);

        return Result.Success(phase);
    }

    /// <summary>
    /// Updates the details of an existing phase. Only allowed when the lifecycle is in the Proposed state.
    /// </summary>
    public Result UpdatePhase(Guid phaseId, string name, string description)
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Phases can only be updated on proposed lifecycles.");
        }

        var phase = _phases.FirstOrDefault(p => p.Id == phaseId);
        if (phase is null)
        {
            return Result.Failure("Phase not found.");
        }

        return phase.Update(name, description);
    }

    /// <summary>
    /// Removes a phase from the lifecycle and reorders remaining phases.
    /// Only allowed when the lifecycle is in the Proposed state.
    /// </summary>
    public Result RemovePhase(Guid phaseId)
    {
        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Phases can only be removed from proposed lifecycles.");
        }

        var phase = _phases.FirstOrDefault(p => p.Id == phaseId);
        if (phase is null)
        {
            return Result.Failure("Phase not found.");
        }

        _phases.Remove(phase);

        ReorderPhases();

        return Result.Success();
    }

    /// <summary>
    /// Reorders the phases based on the provided ordered list of phase IDs.
    /// Only allowed when the lifecycle is in the Proposed state.
    /// </summary>
    /// <param name="orderedPhaseIds">The phase IDs in the desired order.</param>
    public Result ReorderPhases(List<Guid> orderedPhaseIds)
    {
        Guard.Against.Null(orderedPhaseIds, nameof(orderedPhaseIds));

        if (State != ProjectLifecycleState.Proposed)
        {
            return Result.Failure("Phases can only be reordered on proposed lifecycles.");
        }

        if (orderedPhaseIds.Count != _phases.Count)
        {
            return Result.Failure("The number of phase IDs must match the number of existing phases.");
        }

        if (orderedPhaseIds.Distinct().Count() != orderedPhaseIds.Count)
        {
            return Result.Failure("Duplicate phase IDs are not allowed.");
        }

        for (int i = 0; i < orderedPhaseIds.Count; i++)
        {
            var phase = _phases.FirstOrDefault(p => p.Id == orderedPhaseIds[i]);
            if (phase is null)
            {
                return Result.Failure($"Phase with ID '{orderedPhaseIds[i]}' not found.");
            }

            phase.Order = i + 1;
        }

        return Result.Success();
    }

    /// <summary>
    /// Resets phase ordering to eliminate gaps after removal.
    /// </summary>
    private void ReorderPhases()
    {
        int order = 1;
        foreach (var phase in _phases.OrderBy(p => p.Order))
        {
            phase.Order = order;
            order++;
        }
    }

    #endregion Phase Management

    /// <summary>
    /// Creates a new project lifecycle in the Proposed state.
    /// </summary>
    /// <param name="name">The name of the lifecycle.</param>
    /// <param name="description">A description of the lifecycle's purpose and use cases.</param>
    /// <param name="phases">Optional initial phases to include. Each tuple contains (name, description).</param>
    /// <returns>A new ProjectLifecycle instance.</returns>
    public static ProjectLifecycle Create(string name, string description, IEnumerable<(string Name, string Description)>? phases = null)
    {
        var lifecycle = new ProjectLifecycle(name, description);

        if (phases is not null)
        {
            int order = 1;
            foreach (var (phaseName, phaseDescription) in phases)
            {
                lifecycle._phases.Add(new ProjectLifecyclePhase(lifecycle.Id, phaseName, phaseDescription, order));
                order++;
            }
        }

        return lifecycle;
    }
}
