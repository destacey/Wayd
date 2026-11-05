using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Notifications;

/// <summary>
/// Why a person was told about a change: the relationship to the record that put them in its audience.
/// </summary>
/// <remarks>
/// A set rather than a choice: someone who follows a project and also manages it is told once, for both
/// reasons, and seeing both is what lets them understand why muting one does not silence the record.
/// Flags are how the set is stored; every boundary outside the domain carries it as a collection of names.
/// <para>
/// Every member is part of the published contract, including those nothing produces yet.
/// <strong>Never renumber a member</strong>: the values are persisted on every notification and reason mute.
/// </para>
/// <para>
/// No <c>None</c>. A notification exists because of at least one reason, so zero is never a valid value.
/// </para>
/// </remarks>
[Flags]
public enum NotificationReason
{
    [Display(Name = "Following", Description = "You follow the record.", Order = 1)]
    Following = 1,

    [Display(Name = "Requester", Description = "You asked for the record.", Order = 2)]
    Requester = 2,

    [Display(Name = "Participant", Description = "You were given the record and can join its conversation.", Order = 3)]
    Participant = 4,

    [Display(Name = "Assigned", Description = "The record is assigned to you.", Order = 4)]
    Assigned = 8,

    [Display(Name = "Mentioned", Description = "You were mentioned on the record.", Order = 5)]
    Mentioned = 16,

    [Display(Name = "Desk", Description = "The record is waiting, unassigned, on a desk you work on.", Order = 6)]
    Desk = 32,

    [Display(Name = "Role", Description = "You hold a role on the record, or belong to the team that owns it.", Order = 7)]
    Role = 64,

    [Display(Name = "Thread", Description = "You started or replied in a conversation thread on the record.", Order = 8)]
    Thread = 128,
}

/// <summary>Checks on a <see cref="NotificationReason"/> set.</summary>
public static class NotificationReasons
{
    /// <summary>Every defined reason combined.</summary>
    public const NotificationReason All =
        NotificationReason.Following
        | NotificationReason.Requester
        | NotificationReason.Participant
        | NotificationReason.Assigned
        | NotificationReason.Mentioned
        | NotificationReason.Desk
        | NotificationReason.Role
        | NotificationReason.Thread;

    /// <summary>
    /// The reasons something tells people for today, and so the only reasons that can be muted.
    /// </summary>
    /// <remarks>
    /// A mute of a reason nothing produces would sit unnoticed until a producer arrived, then silently swallow
    /// its first notifications. A producer of a new reason adds it here.
    /// </remarks>
    public const NotificationReason Produced =
        NotificationReason.Following
        | NotificationReason.Requester
        | NotificationReason.Participant
        | NotificationReason.Assigned
        | NotificationReason.Mentioned
        | NotificationReason.Role
        | NotificationReason.Thread;

    /// <summary>Whether the set holds at least one reason and nothing that is not a defined reason.</summary>
    public static bool IsValid(NotificationReason reasons) => reasons != 0 && (reasons & ~All) == 0;

    /// <summary>Whether the value is exactly one reason that is produced today, so it can be muted.</summary>
    public static bool IsMutable(NotificationReason reason) =>
        reason != 0 && (reason & (reason - 1)) == 0 && (reason & ~Produced) == 0;
}
