using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Common.Domain.Following;

/// <summary>
/// One person's choice to be told about one record.
/// </summary>
/// <remarks>
/// <strong>A follow grants nothing.</strong> It records intent and is never an input to authorization:
/// nothing may widen a read because a follow exists, and a record its follower can no longer read simply
/// drops out of their list. It is also unrelated to participation — a participant is <em>given</em> a
/// record and the access that goes with it; a follower chose one they could already see.
/// <para>
/// Keyed by (<see cref="OwnerType"/>, <see cref="OwnerId"/>, <see cref="EmployeeId"/>) with no surrogate
/// id, so following twice cannot produce two rows. <see cref="OwnerId"/> is deliberately not a foreign
/// key — it points at a different table per owner type — so a hard-deleted record leaves its follows
/// behind. They match nothing on read and so never surface.
/// </para>
/// <para>
/// Raises no events: it is a person's own subscription, not history of the record it points at.
/// </para>
/// </remarks>
public sealed class Follow
{
    private Follow() { }

    private Follow(string ownerType, Guid ownerId, Guid employeeId, Instant followedOn)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        EmployeeId = employeeId;
        FollowedOn = followedOn;
    }

    /// <summary>
    /// Creates a follow of a record by an employee, failing when the owner type is not registered.
    /// </summary>
    /// <remarks>
    /// Says nothing about whether the employee may read the record. That is the caller's check, made
    /// through the record's own read path before a follow is created.
    /// </remarks>
    public static Result<Follow> Create(string ownerType, Guid ownerId, Guid employeeId, Instant followedOn)
    {
        var descriptor = FollowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<Follow>(descriptor.Error);
        }

        Guard.Against.Default(ownerId, nameof(ownerId));
        Guard.Against.Default(employeeId, nameof(employeeId));

        // The registered key rather than the caller's spelling, so a follow is stored under one casing.
        return new Follow(descriptor.Value.Key, ownerId, employeeId, followedOn);
    }

    /// <summary>The registered owner type of the followed record.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>The followed record's id, unique only within <see cref="OwnerType"/>.</summary>
    public Guid OwnerId { get; private init; }

    /// <summary>The employee who follows the record.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>When the employee started following the record.</summary>
    public Instant FollowedOn { get; private init; }
}
