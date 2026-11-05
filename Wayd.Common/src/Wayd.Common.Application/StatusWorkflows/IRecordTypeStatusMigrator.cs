using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Moves the records of one record type, in a set of scopes, onto the workflow a remap targets — what a
/// scheme change needs where <see cref="IStatusRecordMigrator"/> moves every record on a workflow.
/// </summary>
/// <remarks>
/// Implemented by the module that owns an owner type with record types, since only it knows which scope and
/// type each record has. Until an owner type has one, any scheme change that would move its records is
/// refused rather than applied to the scheme alone. Unmarked and registered explicitly, for the same
/// reasons as <see cref="IStatusRecordMigrator"/>.
/// </remarks>
public interface IRecordTypeStatusMigrator
{
    /// <summary>The owner type whose records this migrator moves.</summary>
    string OwnerType { get; }

    /// <summary>
    /// Applies the remap to the records on <see cref="StatusRemap.FromWorkflowId"/> that the selection names.
    /// </summary>
    /// <param name="selection">Which records: their scopes and their record type.</param>
    /// <returns>How many records moved.</returns>
    /// <remarks>
    /// Changes tracked records only and never saves, nor writes set-based: the scheme command saves the records
    /// together with the scheme's own change, and discards both when either is refused. Safe to re-run, as
    /// <c>SwitchWorkflow</c> is a no-op for a record already moved.
    /// </remarks>
    Task<Result<int>> Migrate(
        StatusRemap remap, RecordSelection selection, EventActor actor, Instant timestamp, CancellationToken cancellationToken);

    /// <summary>Counts the selected records on each status of a workflow, for a preview.</summary>
    Task<Result<IReadOnlyDictionary<Guid, int>>> CountByStatus(
        Guid workflowId, RecordSelection selection, CancellationToken cancellationToken);
}

/// <summary>The records a scheme change moves.</summary>
/// <remarks>
/// Assumes one level of scope below the organization. Under a <see cref="WorkflowScopeChain"/> with nested
/// scopes, a scope's assignment also governs the records of narrower scopes that have none of their own, and
/// the organization's governs only records with no assigned scope anywhere in their chain; this selection does
/// not express either, so it must change before an owner type with nested scopes registers a migrator.
/// </remarks>
/// <param name="ScopeIds">
/// The scopes whose assignment names the scheme. <c>null</c> among them is the organization-level
/// assignment: records in every scope without an assignment of its own.
/// </param>
/// <param name="RecordTypeId">
/// The record type to move, or <c>null</c> for records that follow the scheme's default: those with no type,
/// and those whose type is not in <paramref name="ExcludedRecordTypeIds"/>.
/// </param>
/// <param name="ExcludedRecordTypeIds">
/// With a <c>null</c> <paramref name="RecordTypeId"/>, the types that have an entry and so do not follow the
/// default. Empty otherwise.
/// </param>
public sealed record RecordSelection(
    IReadOnlyCollection<Guid?> ScopeIds,
    Guid? RecordTypeId,
    IReadOnlyCollection<Guid> ExcludedRecordTypeIds);
