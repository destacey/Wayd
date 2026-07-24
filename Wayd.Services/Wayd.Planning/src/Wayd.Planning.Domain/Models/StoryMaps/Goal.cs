using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A goal on a Story Map — what the user is trying to accomplish. Goals form the top row, read left
/// to right as a narrative, and each holds the steps that describe how the goal is accomplished.
/// A goal must always keep at least one step.
/// </summary>
public sealed class Goal : BaseAuditableEntity
{
    private readonly List<Guid> _personaIds = [];
    private readonly List<Step> _steps = [];

    private Goal() { }

    internal Goal(Guid storyMapId, string name, int sortOrder)
    {
        StoryMapId = storyMapId;
        Name = name;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// The Story Map this goal belongs to.
    /// </summary>
    public Guid StoryMapId { get; private init; }

    /// <summary>
    /// The name of the goal.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// The order of the goal within the map.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// The personas tagged on this goal.
    /// </summary>
    public IReadOnlyList<Guid> PersonaIds => _personaIds.AsReadOnly();

    /// <summary>
    /// The steps beneath this goal, in order.
    /// </summary>
    public IReadOnlyList<Step> Steps => [.. _steps.OrderBy(x => x.SortOrder)];

    internal void Rename(string name) => Name = name;

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    #region Personas

    internal void SetPersonas(IEnumerable<Guid> personaIds)
    {
        _personaIds.Clear();
        _personaIds.AddRange(personaIds.Distinct());
    }

    internal void RemovePersona(Guid personaId) => _personaIds.Remove(personaId);

    #endregion Personas

    #region Steps

    internal Step AddStep(string name)
    {
        int nextOrder = _steps.Count > 0 ? _steps.Max(x => x.SortOrder) + 1 : 0;
        var step = new Step(Id, name, nextOrder);
        _steps.Add(step);
        return step;
    }

    internal void AttachStep(Step step) => _steps.Add(step);

    internal Result<Step> GetStep(Guid stepId)
    {
        var step = _steps.FirstOrDefault(x => x.Id == stepId);
        return step is not null
            ? step
            : Result.Failure<Step>("Step does not exist on this goal.");
    }

    /// <summary>
    /// Removes a step from the goal. A goal must keep at least one step, so removing the last one
    /// fails. Returns the removed step so the aggregate can clean up its tasks.
    /// </summary>
    internal Result<Step> RemoveStep(Guid stepId)
    {
        var step = _steps.FirstOrDefault(x => x.Id == stepId);
        if (step is null)
            return Result.Failure<Step>("Step does not exist on this goal.");

        if (_steps.Count == 1)
            return Result.Failure<Step>("A goal must have at least one step.");

        _steps.Remove(step);
        ResetStepOrder();
        return step;
    }

    /// <summary>
    /// Detaches a step without the last-step guard. Used when a step is moved to another goal.
    /// </summary>
    internal Result DetachStep(Step step)
    {
        if (!_steps.Contains(step))
            return Result.Failure("Step does not exist on this goal.");

        _steps.Remove(step);
        ResetStepOrder();
        return Result.Success();
    }

    internal int NextStepOrder() => _steps.Count > 0 ? _steps.Max(x => x.SortOrder) + 1 : 0;

    internal void ResetStepOrder()
    {
        int i = 0;
        foreach (var step in _steps.OrderBy(x => x.SortOrder).ToList())
        {
            step.SetSortOrder(i);
            i++;
        }
    }

    internal bool HasStep(Guid stepId) => _steps.Any(x => x.Id == stepId);

    internal int StepCount => _steps.Count;

    #endregion Steps
}
