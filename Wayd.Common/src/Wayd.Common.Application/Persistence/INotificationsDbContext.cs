using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Persistence;

/// <summary>
/// The tables of the <c>Notifications</c> schema: who follows what, and who was told about which change,
/// across every followable owner type.
/// </summary>
/// <remarks>
/// Separate from <see cref="IWaydDbContext"/> for the same reason as <see cref="IStatusWorkflowDbContext"/>:
/// the modules that make their records followable read it alongside their own context, and their test
/// fakes should not owe the whole global interface for one set.
/// </remarks>
public interface INotificationsDbContext
{
    /// <summary>
    /// Every follow. Reads are keyed by (<c>OwnerType</c>, <c>OwnerId</c>) or by <c>EmployeeId</c> —
    /// <c>OwnerId</c> alone is only unique within an owner type.
    /// </summary>
    /// <remarks>
    /// Read through this set; write through <c>IFollowStore</c>, which turns a concurrent duplicate follow
    /// into a no-op rather than a failed save.
    /// </remarks>
    DbSet<Follow> Follows { get; }

    /// <summary>
    /// Every notification. Reads are keyed by <c>RecipientEmployeeId</c>; <c>OwnerId</c> is only unique within
    /// an owner type.
    /// </summary>
    /// <remarks>
    /// Read through this set; write through <c>INotificationStore</c>, which makes a redelivered event write
    /// nothing twice and marks read set-based.
    /// </remarks>
    DbSet<Notification> Notifications { get; }

    /// <summary>
    /// Every record mute. Reads are keyed by (<c>OwnerType</c>, <c>OwnerId</c>) or by <c>EmployeeId</c>.
    /// </summary>
    /// <remarks>
    /// Read through this set; write through <c>IMuteStore</c>, which makes muting twice a no-op.
    /// </remarks>
    DbSet<RecordMute> RecordMutes { get; }

    /// <summary>Every reason mute. Reads are keyed by <c>EmployeeId</c>.</summary>
    /// <remarks>Read through this set; write through <c>IMuteStore</c>.</remarks>
    DbSet<ReasonMute> ReasonMutes { get; }
}
