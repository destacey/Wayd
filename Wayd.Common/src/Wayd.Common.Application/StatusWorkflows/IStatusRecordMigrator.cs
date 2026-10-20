using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Moves every record of one owner type onto the workflow a remap targets.
/// </summary>
/// <remarks>
/// The engine lives in Common, but the records it governs belong to the modules — Products, Releases,
/// Release Packages and Deployments today. Common has no reference to those projects and must not gain
/// one, so reassignment asks through this seam instead of reaching for tables it cannot see.
/// <para>
/// <strong>Deliberately unmarked.</strong> It carries neither <c>IScopedService</c> nor
/// <c>ITransientService</c>, for two reasons. The convention-based registration scan binds one
/// implementation per interface, so four migrators sharing this one would silently collapse to
/// whichever was scanned last. And an unmarked abstraction is not a "service" as the layering rule
/// defines one, so a handler injecting a collection of these is not depending on a peer. Registration
/// is explicit, in Infrastructure, beside the owner-type registration it mirrors.
/// </para>
/// </remarks>
public interface IStatusRecordMigrator
{
    /// <summary>The owner type whose records this migrator moves.</summary>
    string OwnerType { get; }

    /// <summary>
    /// Applies the remap to every record currently on <see cref="StatusRemap.FromWorkflowId"/>.
    /// </summary>
    /// <remarks>
    /// Safe to re-run: <c>SwitchWorkflow</c> is a no-op for a record already on the target workflow, so
    /// an interrupted migration can simply be repeated.
    /// <para>
    /// Fails for a non-null <paramref name="scopeId"/> until a record type can say which scope it
    /// belongs to: records are selected by workflow alone, so a scoped migration would move every
    /// record on the workflow rather than only that scope's.
    /// </para>
    /// </remarks>
    /// <returns>How many records moved.</returns>
    Task<Result<int>> Migrate(
        StatusRemap remap,
        Guid? scopeId,
        EventActor actor,
        Instant timestamp,
        CancellationToken cancellationToken);
}

/// <summary>
/// Counts how many records sit on each status of a workflow.
/// </summary>
/// <remarks>
/// Separate from <see cref="IStatusRecordMigrator"/> so counting for a preview never travels on the
/// interface that performs the migration. The reassignment screen uses this to show the blast radius
/// per status before anything is committed.
/// </remarks>
public interface IStatusRecordCounter
{
    /// <summary>The owner type whose records this counter reads.</summary>
    string OwnerType { get; }

    /// <summary>Record counts keyed by status id. Statuses holding nothing may be absent.</summary>
    /// <remarks>
    /// Fails for a non-null <paramref name="scopeId"/>, for the same reason as
    /// <see cref="IStatusRecordMigrator.Migrate"/>: a scoped count would report every record on the
    /// workflow.
    /// </remarks>
    Task<Result<IReadOnlyDictionary<Guid, int>>> CountByStatus(
        Guid workflowId,
        Guid? scopeId,
        CancellationToken cancellationToken);
}
