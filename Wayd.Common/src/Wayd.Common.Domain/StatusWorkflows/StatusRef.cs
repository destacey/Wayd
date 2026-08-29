using Ardalis.GuardClauses;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A status already resolved from its workflow, ready to be stored on a record: the id to keep, the
/// category to denormalize alongside it, and the alias it carries.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so an aggregate can hold a configurable status without depending on the workflow
/// engine at write time. The application layer loads the workflow, resolves the target status, and
/// hands the aggregate this; the aggregate stores the id and the category together and never needs the
/// workflow again to answer a question about itself.
/// </para>
/// <para>
/// Denormalizing the category is deliberate. The alternative — resolving it through the workflow on
/// every read — makes every invariant and every projection depend on the workflow being loaded, which
/// is the same silent failure mode that makes PPM's ancestry authorization hard to work in: a missing
/// include produces a wrong answer rather than an error.
/// </para>
/// </remarks>
public sealed record StatusRef
{
    public StatusRef(Guid statusId, StatusCategory category, int alias = StatusWorkflow.NoAlias)
    {
        Guard.Against.Default(statusId, nameof(statusId));

        StatusId = statusId;
        Category = category;
        Alias = alias;
    }

    /// <summary>The status the record holds. Stable across renames.</summary>
    public Guid StatusId { get; }

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

        return new StatusRef(status.Id, status.Category, status.Alias);
    }
}
