using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Domain.Notifications;

/// <summary>
/// One person's choice to hear nothing more about one record, whatever the reason they would be told.
/// </summary>
/// <remarks>
/// <strong>A mute changes only what is written to its owner's notifications.</strong> It is never an input
/// to authorization or to participation: a participant who mutes a record keeps their role and access, and
/// a follower who mutes a record still follows it. It is independent of <see cref="Follow"/> in both
/// directions — muting needs no follow, and unfollowing leaves a mute in place.
/// <para>
/// Keyed by (<see cref="OwnerType"/>, <see cref="OwnerId"/>, <see cref="EmployeeId"/>) with no surrogate id,
/// so muting twice cannot produce two rows. <see cref="OwnerId"/> is not a foreign key, as for follows, so a
/// hard-deleted record leaves its mutes behind, matching nothing.
/// </para>
/// <para>Raises no events: it is a person's own preference, not history of the record.</para>
/// </remarks>
public sealed class RecordMute
{
    private RecordMute() { }

    private RecordMute(string ownerType, Guid ownerId, Guid employeeId, Instant mutedOn)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        EmployeeId = employeeId;
        MutedOn = mutedOn;
    }

    /// <summary>
    /// Creates a mute of a record by an employee, failing when the owner type is not registered.
    /// </summary>
    /// <remarks>
    /// Says nothing about whether the employee may read the record; the caller checks that through the
    /// record's own read rule first, so a mute reveals nothing.
    /// </remarks>
    public static Result<RecordMute> Create(string ownerType, Guid ownerId, Guid employeeId, Instant mutedOn)
    {
        var descriptor = FollowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<RecordMute>(descriptor.Error);
        }

        Guard.Against.Default(ownerId, nameof(ownerId));
        Guard.Against.Default(employeeId, nameof(employeeId));

        // The registered key rather than the caller's spelling, so a mute is stored under one casing.
        return new RecordMute(descriptor.Value.Key, ownerId, employeeId, mutedOn);
    }

    /// <summary>The registered owner type of the muted record.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>The muted record's id, unique only within <see cref="OwnerType"/>.</summary>
    public Guid OwnerId { get; private init; }

    /// <summary>The employee who muted the record.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>When the employee muted the record.</summary>
    public Instant MutedOn { get; private init; }
}
