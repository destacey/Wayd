using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A task on a Story Map — the individual pieces of work that make a step possible. Tasks are the
/// cards that fill the grid. A task belongs to exactly one step and one swim lane, can be tagged
/// with personas, can hold a short checklist, and can optionally reference a work item that already
/// exists elsewhere in Wayd.
/// </summary>
public sealed class StoryMapTask : BaseAuditableEntity
{
    private readonly List<Guid> _personaIds = [];
    private readonly List<ChecklistItem> _checklist = [];

    private StoryMapTask() { }

    internal StoryMapTask(Guid stepId, Guid swimLaneId, string title, int order)
    {
        StepId = stepId;
        SwimLaneId = swimLaneId;
        Title = title;
        Order = order;
    }

    /// <summary>
    /// The step this task sits beneath.
    /// </summary>
    public Guid StepId { get; private set; }

    /// <summary>
    /// The swim lane this task belongs to.
    /// </summary>
    public Guid SwimLaneId { get; private set; }

    /// <summary>
    /// The title of the task.
    /// </summary>
    public string Title
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Title)).Trim();
    } = default!;

    /// <summary>
    /// Free-form description of the task.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = value.NullIfWhiteSpacePlusTrim();
    }

    /// <summary>
    /// The order of the task within its (step, swim lane) cell.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// The id of a work item elsewhere in Wayd that this task references. The map never modifies
    /// what it links to — this is a reference, not an integration. Type and state are read through
    /// at display time.
    /// </summary>
    public int? LinkedWorkItemId { get; private set; }

    /// <summary>
    /// The personas tagged on this task.
    /// </summary>
    public IReadOnlyList<Guid> PersonaIds => _personaIds.AsReadOnly();

    /// <summary>
    /// The task's checklist items.
    /// </summary>
    public IReadOnlyList<ChecklistItem> Checklist => [.. _checklist.OrderBy(x => x.Order)];

    internal void UpdateDetails(string title, string? description)
    {
        Title = title;
        Description = description;
    }

    internal void SetOrder(int order) => Order = order;

    internal void MoveTo(Guid stepId, Guid swimLaneId, int order)
    {
        StepId = stepId;
        SwimLaneId = swimLaneId;
        Order = order;
    }

    internal void ReassignSwimLane(Guid swimLaneId, int order)
    {
        SwimLaneId = swimLaneId;
        Order = order;
    }

    #region Personas

    internal void SetPersonas(IEnumerable<Guid> personaIds)
    {
        _personaIds.Clear();
        _personaIds.AddRange(personaIds.Distinct());
    }

    internal void RemovePersona(Guid personaId) => _personaIds.Remove(personaId);

    #endregion Personas

    #region Checklist

    /// <summary>
    /// The count of checked items and total items, shown on the card as "3/5".
    /// </summary>
    public (int Completed, int Total) CompletionCount => (_checklist.Count(x => x.IsChecked), _checklist.Count);

    internal Result<ChecklistItem> AddChecklistItem(string name)
    {
        try
        {
            int nextOrder = _checklist.Count > 0 ? _checklist.Max(x => x.Order) + 1 : 0;
            var item = new ChecklistItem(name, nextOrder);
            _checklist.Add(item);
            return item;
        }
        catch (Exception ex)
        {
            return Result.Failure<ChecklistItem>(ex.Message);
        }
    }

    internal Result RenameChecklistItem(Guid itemId, string name)
    {
        var item = _checklist.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
            return Result.Failure("Checklist item does not exist on this task.");

        try
        {
            item.Rename(name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    internal Result SetChecklistItemChecked(Guid itemId, bool isChecked)
    {
        var item = _checklist.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
            return Result.Failure("Checklist item does not exist on this task.");

        item.SetChecked(isChecked);
        return Result.Success();
    }

    internal Result RemoveChecklistItem(Guid itemId)
    {
        var item = _checklist.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
            return Result.Failure("Checklist item does not exist on this task.");

        _checklist.Remove(item);
        ResetChecklistOrder();
        return Result.Success();
    }

    /// <summary>
    /// Removes a checklist item and returns its name, so the caller can create a task from it. Used
    /// when promoting a checklist item that turned out to really be a task.
    /// </summary>
    internal Result<string> PromoteChecklistItem(Guid itemId)
    {
        var item = _checklist.FirstOrDefault(x => x.Id == itemId);
        if (item is null)
            return Result.Failure<string>("Checklist item does not exist on this task.");

        var name = item.Name;
        _checklist.Remove(item);
        ResetChecklistOrder();
        return name;
    }

    private void ResetChecklistOrder()
    {
        int i = 0;
        foreach (var item in _checklist.OrderBy(x => x.Order).ToList())
        {
            item.SetOrder(i);
            i++;
        }
    }

    #endregion Checklist

    #region Work item link

    internal void LinkWorkItem(int workItemId) => LinkedWorkItemId = workItemId;

    internal void UnlinkWorkItem() => LinkedWorkItemId = null;

    #endregion Work item link
}
