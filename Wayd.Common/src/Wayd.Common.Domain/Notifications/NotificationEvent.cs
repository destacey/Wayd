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
}
