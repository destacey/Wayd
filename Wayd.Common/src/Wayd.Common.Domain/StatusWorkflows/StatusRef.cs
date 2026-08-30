using Ardalis.GuardClauses;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A status already resolved from its workflow, ready to be stored on a record: which workflow it came
/// from, the id to keep, the name and category to freeze alongside it, and the alias it carries.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so an aggregate can hold a configurable status without depending on the workflow
/// engine at write time. The application layer loads the workflow, resolves the target status, and
/// hands the aggregate this; the aggregate stores what it needs and never consults the workflow again.
/// </para>
/// <para>
/// Denormalizing the category is deliberate. The alternative — resolving it through the workflow on
/// every read — makes every invariant and every projection depend on the workflow being loaded, which
/// is the same silent failure mode that makes PPM's ancestry authorization hard to work in: a missing
/// include produces a wrong answer rather than an error.
/// </para>
/// <para>
/// <see cref="Name"/> travels with the reference because a status can be renamed while its workflow is
/// active. Records and transitions freeze it, so a rename cannot rewrite what past rows read as.
/// </para>
/// </remarks>
public sealed record StatusRef
{
    public StatusRef(Guid workflowId, Guid statusId, string name, StatusCategory category, int alias = StatusWorkflow.NoAlias)
    {
        WorkflowId = Guard.Against.Default(workflowId, nameof(workflowId));
        StatusId = Guard.Against.Default(statusId, nameof(statusId));
        Name = Guard.Against.NullOrWhiteSpace(name, nameof(name)).Trim();
        Category = category;
        Alias = alias;
    }

    /// <summary>
    /// The workflow this status came from, so a record can say which workflow governed it rather than
    /// leaving it to be inferred from a status row that may later be deleted.
    /// </summary>
    public Guid WorkflowId { get; }

    /// <summary>The status the record holds. Stable across renames.</summary>
    public Guid StatusId { get; }

    /// <summary>What the status was called when it was resolved. Frozen by whoever stores it.</summary>
    public string Name { get; }

    /// <summary>The status's bucket, stored alongside the id so reads never need the workflow.</summary>
    public StatusCategory Category { get; }

    /// <summary>
    /// The well-known meaning the status carries, or <see cref="StatusWorkflow.NoAlias"/>. An
    /// <c>int</c> because the meanings belong to the consuming module rather than to the engine — the
    /// caller casts to its own alias enum.
    /// </summary>
    public int Alias { get; }

    /// <summary>
    /// Builds a reference from a workflow status.
    /// </summary>
    public static StatusRef From(WorkflowStatus status)
    {
        Guard.Against.Null(status, nameof(status));

        return new StatusRef(status.WorkflowId, status.Id, status.Name, status.Category, status.Alias);
    }
}
