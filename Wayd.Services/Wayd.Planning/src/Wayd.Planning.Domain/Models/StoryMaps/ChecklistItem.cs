using Ardalis.GuardClauses;

namespace Wayd.Planning.Domain.Models.StoryMaps;

/// <summary>
/// A single checklist item within a <see cref="StoryMapTask"/>. Checklist items are the "and don't
/// forget…" notes that come up while scoping a task. They live inside the task, never appear on the
/// grid as their own cards, and are deliberately limited — they cannot be tagged with personas,
/// assigned to a swim lane, or nested. If an item needs any of those things, it should be promoted to a
/// task instead.
/// </summary>
public sealed class ChecklistItem
{
    private ChecklistItem() { }

    internal ChecklistItem(string name, int order)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Order = order;
        IsChecked = false;
    }

    /// <summary>
    /// A stable identity for the item so it can be individually checked, renamed, promoted, or
    /// removed. Persisted inside the owning task's JSON payload.
    /// </summary>
    public Guid Id { get; private init; }

    /// <summary>
    /// The name of the checklist item.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// Whether the item has been checked off.
    /// </summary>
    public bool IsChecked { get; private set; }

    /// <summary>
    /// The order of the item within the task's checklist.
    /// </summary>
    public int Order { get; private set; }

    internal void Rename(string name) => Name = name;

    internal void SetChecked(bool isChecked) => IsChecked = isChecked;

    internal void SetOrder(int order) => Order = order;
}
