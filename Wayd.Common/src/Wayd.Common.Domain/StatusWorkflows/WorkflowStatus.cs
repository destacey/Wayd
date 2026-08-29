using Ardalis.GuardClauses;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// One status within a <see cref="StatusWorkflow"/> — what an organization calls a state, plus the two
/// machine-readable facts the domain actually reasons about: its <see cref="Category"/> and its
/// optional <see cref="Alias"/>.
/// </summary>
/// <remarks>
/// The <see cref="BaseEntity{TId}.Id"/> is what records store and is stable across renames, which is
/// the whole point: renaming "Rolled Back" to "Reverted" changes one row and touches no deployment.
/// Statuses are created and ordered through the owning <see cref="StatusWorkflow"/>, never directly.
/// </remarks>
public sealed class WorkflowStatus : BaseAuditableEntity
{
    private WorkflowStatus() { }

    internal WorkflowStatus(Guid workflowId, string name, string? description, StatusCategory category, int alias, int order, bool isSystem = false)
    {
        WorkflowId = workflowId;
        Name = name;
        Description = description;
        Category = category;
        Alias = alias;
        Order = order;
        IsSystem = isSystem;
    }

    /// <summary>
    /// The workflow this status belongs to. A status is never shared between workflows — cloning a
    /// workflow copies its statuses, so one organization's edits cannot reach another's records.
    /// </summary>
    public Guid WorkflowId { get; private init; }

    /// <summary>
    /// What the organization calls this status. Freely renamable; nothing binds to it.
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// Optional explanation shown to administrators choosing between statuses.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The bucket this status belongs to. Queries and rollups group on this, so it is the one field a
    /// caller can rely on without knowing anything about the workflow.
    /// </summary>
    public StatusCategory Category { get; private set; }

    /// <summary>
    /// The well-known meaning this status carries, or <see cref="StatusWorkflow.NoAlias"/>. Unique
    /// within a workflow. Domain invariants and metrics bind here rather than to <see cref="Name"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately an <c>int</c> rather than an enum: the meanings are the consuming module's
    /// vocabulary, not the engine's. Product Management casts to
    /// <c>Enums.ProductManagement.ProductStatusAlias</c> at its own boundary, and a module adopting the
    /// engine later brings its own enum without this type changing. The engine stores and compares the
    /// value; it never interprets it.
    /// </remarks>
    public int Alias { get; private set; }

    /// <summary>
    /// Display position within the workflow. Presentation only — it carries no lifecycle meaning, and
    /// no transition rule may be inferred from two statuses' relative order.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether this status is one the platform seeded, and therefore cannot be deleted or have its
    /// alias moved away. Set when the workflow is seeded from a system default.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Renames a status and reworks its description. Always safe: records hold the id, not the name.
    /// </summary>
    internal void Rename(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Changes which bucket this status belongs to. Destructive when records already hold it — their
    /// denormalized category becomes stale until they are remapped — so the workflow, not the status,
    /// decides whether this is allowed.
    /// </summary>
    internal void Reclassify(StatusCategory category) => Category = category;

    /// <summary>
    /// Moves the well-known meaning onto or off this status. The workflow enforces uniqueness.
    /// </summary>
    internal void SetAlias(int alias) => Alias = alias;

    /// <summary>
    /// Repositions the status for display.
    /// </summary>
    internal void Reorder(int order) => Order = order;
}
