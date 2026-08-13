using Ardalis.GuardClauses;
using Wayd.Common.Domain.Employees;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using NodaTime;

namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// An immutable record of a single project status transition. Appended by the <see cref="Project"/>
/// aggregate itself whenever the status changes, so a transition cannot occur without being recorded.
///
/// A row always represents movement: <see cref="FromStatus"/> and <see cref="ToStatus"/> can never be
/// equal. The constructor enforces this, so no caller — the aggregate, or a backfill reconstructing
/// history from another source — can record a transition that never happened.
/// </summary>
public sealed class ProjectStatusHistory : BaseEntity
{
    private ProjectStatusHistory() { }

    internal ProjectStatusHistory(
        Guid projectId,
        ProjectStatus? fromStatus,
        ProjectStatus toStatus,
        string changedByUserId,
        Guid? changedByEmployeeId,
        Instant changedOn,
        ProjectStatusHistorySource source,
        string? reason)
    {
        Guard.Against.Default(projectId, nameof(projectId));
        Guard.Against.NullOrWhiteSpace(changedByUserId, nameof(changedByUserId));

        if (fromStatus == toStatus)
        {
            throw new InvalidOperationException(
                $"Cannot record a status change from {fromStatus} to itself.");
        }

        ProjectId = projectId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByUserId = changedByUserId;
        ChangedByEmployeeId = changedByEmployeeId;
        ChangedOn = changedOn;
        Source = source;
        Reason = reason?.Trim();
    }

    /// <summary>
    /// The project this transition belongs to.
    /// </summary>
    public Guid ProjectId { get; private init; }

    /// <summary>
    /// The status the project moved out of, or null when this row records the project entering its
    /// initial state. A history that legitimately begins mid-life — reconstructed from an audit trail
    /// with no creation record — starts with a non-null value instead.
    /// </summary>
    public ProjectStatus? FromStatus { get; private init; }

    /// <summary>
    /// The status the project moved into.
    /// </summary>
    public ProjectStatus ToStatus { get; private init; }

    /// <summary>
    /// The user account that made the change. Always present; system-initiated transitions are
    /// attributed to the well-known system user.
    /// </summary>
    public string ChangedByUserId { get; private init; } = default!;

    /// <summary>
    /// The employee the acting user was linked to at the moment of the change, or null when there was
    /// none — a system actor, or a user with no employee link. Frozen at write time rather than
    /// resolved on read: the user-to-employee link is mutable, so resolving it later would silently
    /// rewrite history.
    /// </summary>
    public Guid? ChangedByEmployeeId { get; private init; }

    /// <summary>
    /// The employee the acting user was linked to, when one is loaded.
    /// </summary>
    public Employee? ChangedByEmployee { get; private init; }

    /// <summary>
    /// When the transition occurred.
    /// </summary>
    public Instant ChangedOn { get; private init; }

    /// <summary>
    /// How much fidelity this row carries — whether it was recorded as the transition happened or
    /// reconstructed after the fact from the audit trail.
    /// </summary>
    public ProjectStatusHistorySource Source { get; private init; }

    /// <summary>
    /// An optional explanation for the transition.
    /// </summary>
    public string? Reason { get; private init; }
}
