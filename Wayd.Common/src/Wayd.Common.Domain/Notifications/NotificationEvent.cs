using System.ComponentModel.DataAnnotations;

namespace Wayd.Common.Domain.Notifications;

/// <summary>
/// What happened to a record, in the terms a notification list words it in.
/// </summary>
/// <remarks>
/// A closed set rather than the domain event's type name, so the API never publishes internal names such
/// as <c>ProjectStatusChangedEventV2</c>, and a new version of an event maps onto the same member. The
/// event's full payload stays in the activity log, reached through the notification's <c>EventId</c>.
/// <para>
/// Persisted by name. <strong>Never rename a member</strong> once it ships.
/// </para>
/// </remarks>
public enum NotificationEvent
{
    [Display(Name = "Created", Description = "The record was created.", Order = 1)]
    Created = 1,

    [Display(Name = "Status Changed", Description = "The record moved to a different status.", Order = 2)]
    StatusChanged = 2,

    [Display(Name = "Lifecycle Changed", Description = "The record's lifecycle was replaced by a different one.", Order = 3)]
    LifecycleChanged = 3,

    [Display(Name = "Health Reported", Description = "A new health check was recorded.", Order = 4)]
    HealthReported = 4,

    [Display(Name = "Timeline Changed", Description = "The record's dates changed.", Order = 5)]
    TimelineChanged = 5,

    [Display(Name = "Roles Changed", Description = "People were added to or removed from the record's roles.", Order = 6)]
    RolesChanged = 6,

    [Display(Name = "Moved", Description = "The record moved to a different parent.", Order = 7)]
    Reparented = 7,

    [Display(Name = "Scored", Description = "The record was scored.", Order = 8)]
    ScoreRecorded = 8,

    [Display(Name = "Withdrawn", Description = "The person who asked for the record no longer needs it.", Order = 9)]
    Withdrawn = 9,

    [Display(Name = "Retitled", Description = "The record's title changed.", Order = 10)]
    Retitled = 10,

    [Display(Name = "Details Changed", Description = "The record's description or other details changed.", Order = 11)]
    DetailsChanged = 11,

    [Display(Name = "Assignee Changed", Description = "The record was assigned to someone else, or unassigned.", Order = 12)]
    AssigneeChanged = 12,

    [Display(Name = "Participants Changed", Description = "Someone was given access to the record, or lost it.", Order = 13)]
    ParticipantsChanged = 13,

    [Display(Name = "Channel Changed", Description = "Where the record is recorded as coming from changed.", Order = 14)]
    ChannelChanged = 14,

    [Display(Name = "Message Posted", Description = "Someone posted in the record's conversation.", Order = 15)]
    MessagePosted = 15,

    /// <remarks>
    /// Its own member rather than <see cref="MessagePosted"/> with a flag, because a record's read rule narrows
    /// notifications by event: one about a note must reach only those who can read notes.
    /// </remarks>
    [Display(Name = "Internal Note Posted", Description = "Someone added an internal note to the record's conversation.", Order = 16)]
    InternalNotePosted = 16,
}
