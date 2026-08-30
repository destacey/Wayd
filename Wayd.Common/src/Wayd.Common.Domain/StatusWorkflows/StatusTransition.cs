using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// One status change, recorded as it happened. Immutable once written.
/// </summary>
/// <remarks>
/// The history of a record lives here rather than being reconstructed from the workflow, because a
/// workflow only ever describes its <em>current</em> shape: a status can be renamed while active, and
/// once the remap engine exists it can be deleted outright. Both names and ids are frozen at write
/// time, so a later rename cannot rewrite what a past row reads as — the same reason
/// <c>Deployment.EnvironmentCategory</c> is frozen.
/// <para>
/// Storing the id alongside the name is the one place this departs from Jira's changelog, which keeps
/// only strings and so cannot join a historical entry back to a status after a rename.
/// </para>
/// <para>
/// Not tied to any one module: <see cref="OwnerType"/> plus <see cref="RecordId"/> identifies what
/// changed, so a module adopting the engine gets its history without a table of its own.
/// </para>
/// </remarks>
public sealed class StatusTransition : BaseEntity
{
    private StatusTransition() { }

    /// <param name="from">
    /// The status moved out of, or <c>null</c> when the record is entering its initial status.
    /// </param>
    public StatusTransition(
        string ownerType,
        Guid recordId,
        StatusRef? from,
        StatusRef to,
        EventActor actor,
        Instant changedOn,
        int sequence,
        string? reason = null)
    {
        Guard.Against.Null(to, nameof(to));
        Guard.Against.Null(actor, nameof(actor));

        OwnerType = Guard.Against.NullOrWhiteSpace(ownerType, nameof(ownerType)).Trim();
        RecordId = Guard.Against.Default(recordId, nameof(recordId));
        WorkflowId = to.WorkflowId;

        FromStatusId = from?.StatusId;
        FromStatusName = from?.Name;
        FromCategory = from?.Category;

        ToStatusId = to.StatusId;
        ToStatusName = to.Name;
        ToCategory = to.Category;
        ToAlias = to.Alias;

        ActorKind = actor.Kind;
        ActorUserId = actor.UserId;
        ChangedOn = changedOn;
        Sequence = sequence;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>The registered owner type of the record that changed.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>The record that changed. Not a foreign key — one table serves every owner type.</summary>
    public Guid RecordId { get; private init; }

    /// <summary>
    /// The workflow that governed this change.
    /// </summary>
    /// <remarks>
    /// Recorded rather than inferred through <see cref="ToStatusId"/>: a status can be deleted once the
    /// remap engine exists, and a record can be moved to a different workflow, either of which would
    /// break the inference.
    /// </remarks>
    public Guid WorkflowId { get; private init; }

    /// <summary>
    /// The status moved out of, or <c>null</c> when the record entered its initial status.
    /// </summary>
    public Guid? FromStatusId { get; private init; }

    /// <summary>What that status was called at the time.</summary>
    public string? FromStatusName { get; private init; }

    /// <summary>The bucket it was in at the time.</summary>
    public StatusCategory? FromCategory { get; private init; }

    /// <summary>The status moved into.</summary>
    public Guid ToStatusId { get; private init; }

    /// <summary>What that status was called at the time. Frozen — a later rename does not reach it.</summary>
    public string ToStatusName { get; private init; } = default!;

    /// <summary>The bucket it moved into.</summary>
    public StatusCategory ToCategory { get; private init; }

    /// <summary>
    /// The well-known meaning it moved into, or <see cref="StatusWorkflow.NoAlias"/>. Frozen so a
    /// metric computed over history stays stable even if the workflow is later restructured.
    /// </summary>
    public int ToAlias { get; private init; }

    /// <summary>The mechanism that made the change.</summary>
    public EventActorKind ActorKind { get; private init; }

    /// <summary>The account behind it, where there is one.</summary>
    public string? ActorUserId { get; private init; }

    /// <summary>When it happened.</summary>
    public Instant ChangedOn { get; private init; }

    /// <summary>
    /// A monotonic per-record sequence number, ordering the history as it actually happened.
    /// </summary>
    /// <remarks>
    /// The only reliable ordering. <see cref="ChangedOn"/> does not separate rows written in one
    /// <c>SaveChanges</c> — an import can walk a record through several transitions at one instant — and
    /// a v7 GUID orders only to millisecond precision. Nor can rows chain themselves by matching
    /// <see cref="FromStatusId"/> to the previous <see cref="ToStatusId"/>: a withdrawn-then-reinstated
    /// record enters the same status twice, so several rows share a from-status.
    /// <para>
    /// A unique index on (OwnerType, RecordId, Sequence) makes a duplicate impossible: two concurrent
    /// transitions that read the same count collide at insert, so one commits and the other retries.
    /// </para>
    /// </remarks>
    public int Sequence { get; private init; }

    /// <summary>Why the change was made, where a reason was recorded.</summary>
    public string? Reason { get; private init; }
}
