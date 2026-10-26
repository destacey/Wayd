using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Notifications;

/// <summary>
/// Writes record and reason mutes, treating "already muted" and "not muted" as outcomes rather than errors.
/// </summary>
/// <remarks>
/// A seam for the same reason as <c>IFollowStore</c>: two mutes of one thing by one person racing each other
/// both pass an existence check, and only the primary key can say which lost.
/// </remarks>
public interface IMuteStore
{
    /// <summary>Saves the mute unless the same person already muted the same record.</summary>
    /// <returns><c>true</c> when the mute was added; <c>false</c> when it already existed.</returns>
    Task<bool> Add(RecordMute mute, CancellationToken cancellationToken);

    /// <summary>Removes the person's mute of the record, if there is one.</summary>
    /// <returns><c>true</c> when a mute was removed; <c>false</c> when there was none.</returns>
    Task<bool> RemoveRecordMute(string ownerType, Guid ownerId, Guid employeeId, CancellationToken cancellationToken);

    /// <summary>Saves the mute unless the same person already muted the same reason.</summary>
    /// <returns><c>true</c> when the mute was added; <c>false</c> when it already existed.</returns>
    Task<bool> Add(ReasonMute mute, CancellationToken cancellationToken);

    /// <summary>Removes the person's mute of the reason, if there is one.</summary>
    /// <returns><c>true</c> when a mute was removed; <c>false</c> when there was none.</returns>
    Task<bool> RemoveReasonMute(Guid employeeId, NotificationReason reason, CancellationToken cancellationToken);
}
