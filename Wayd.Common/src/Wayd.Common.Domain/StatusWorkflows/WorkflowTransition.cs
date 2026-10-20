using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A move the workflow allows.
/// </summary>
/// <remarks>
/// A record holding <see cref="FromStatusId"/> may move to <see cref="ToStatusId"/>. Not to be confused
/// with <see cref="StatusTransition"/>, a move a record made. Consulted only
/// when the workflow's <see cref="StatusWorkflow.TransitionMode"/> is
/// <see cref="Enums.TransitionMode.Restricted"/>.
/// <para>
/// An entity with its own id rather than a value pair so that conditions on a move can attach to it.
/// Created and removed through the owning <see cref="StatusWorkflow"/>, never directly.
/// </para>
/// </remarks>
public sealed class WorkflowTransition : BaseAuditableEntity
{
    private readonly List<TransitionCondition> _conditions = [];

    private WorkflowTransition() { }

    internal WorkflowTransition(Guid workflowId, Guid fromStatusId, Guid toStatusId, IEnumerable<TransitionConditionKind>? conditions = null)
    {
        WorkflowId = workflowId;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;

        foreach (var kind in conditions ?? [])
        {
            _conditions.Add(new TransitionCondition(Id, kind));
        }
    }

    /// <summary>The workflow that allows this move. Both statuses belong to it.</summary>
    public Guid WorkflowId { get; private init; }

    /// <summary>The status a record moves out of.</summary>
    public Guid FromStatusId { get; private init; }

    /// <summary>The status a record moves into.</summary>
    public Guid ToStatusId { get; private init; }

    /// <summary>What a record needs before making this move. At most one of each kind.</summary>
    public IReadOnlyCollection<TransitionCondition> Conditions => _conditions.AsReadOnly();

    /// <summary>The kinds of <see cref="Conditions"/>, in kind order.</summary>
    public IReadOnlyList<TransitionConditionKind> ConditionKinds() =>
        _conditions.Select(c => c.Kind).Order().ToList();

    /// <summary>
    /// Replaces the conditions with exactly <paramref name="kinds"/>, keeping the ones already held.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    /// <remarks>The owning workflow validates the kinds and raises the event.</remarks>
    internal bool SetConditions(IReadOnlySet<TransitionConditionKind> kinds)
    {
        var removed = _conditions.RemoveAll(c => !kinds.Contains(c.Kind));
        var added = kinds.Where(k => _conditions.All(c => c.Kind != k)).ToList();

        foreach (var kind in added)
        {
            _conditions.Add(new TransitionCondition(Id, kind));
        }

        return removed > 0 || added.Count > 0;
    }
}
