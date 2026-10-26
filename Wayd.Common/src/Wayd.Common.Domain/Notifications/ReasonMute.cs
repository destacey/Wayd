using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;

namespace Wayd.Common.Domain.Notifications;

/// <summary>
/// One person's choice to stop being told for one reason, on every record.
/// </summary>
/// <remarks>
/// Removes only that reason from what the person is told: someone told about a change for two reasons who
/// mutes one is still told, for the other. Like <see cref="RecordMute"/>, it never changes access,
/// participation or following.
/// <para>
/// One row per reason, keyed by (<see cref="EmployeeId"/>, <see cref="Reason"/>), so each mute keeps when it
/// was set. Raises no events.
/// </para>
/// </remarks>
public sealed class ReasonMute
{
    private ReasonMute() { }

    private ReasonMute(Guid employeeId, NotificationReason reason, Instant mutedOn)
    {
        EmployeeId = employeeId;
        Reason = reason;
        MutedOn = mutedOn;
    }

    /// <summary>
    /// Creates a mute of one reason by an employee, failing unless the reason is a single reason that
    /// something produces today (<see cref="NotificationReasons.Produced"/>).
    /// </summary>
    public static Result<ReasonMute> Create(Guid employeeId, NotificationReason reason, Instant mutedOn)
    {
        Guard.Against.Default(employeeId, nameof(employeeId));

        return NotificationReasons.IsMutable(reason)
            ? new ReasonMute(employeeId, reason, mutedOn)
            : Result.Failure<ReasonMute>($"'{reason}' is not a reason that can be muted.");
    }

    /// <summary>The employee who muted the reason.</summary>
    public Guid EmployeeId { get; private init; }

    /// <summary>The single reason muted.</summary>
    public NotificationReason Reason { get; private init; }

    /// <summary>When the employee muted the reason.</summary>
    public Instant MutedOn { get; private init; }
}
