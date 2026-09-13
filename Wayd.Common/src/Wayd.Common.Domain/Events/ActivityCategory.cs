namespace Wayd.Common.Domain.Events;

/// <summary>
/// What kind of occurrence an event records, as the activity log presents it. Declared by each event type
/// through <see cref="IDomainEventDescriptor"/> and stored on the <c>ActivityLogEntry</c>.
/// </summary>
/// <remarks>
/// Stored by name, so a member may be added or reordered but never renamed.
/// </remarks>
public enum ActivityCategory
{
    /// <summary>The record came into existence: created, added, planned, assembled, started.</summary>
    Created = 1,

    /// <summary>
    /// Part of the record changed without moving it along its lifecycle: its details, roles, relationships,
    /// key or classification.
    /// </summary>
    Updated = 2,

    /// <summary>The record's dates moved: a timeline, a date range, a target date.</summary>
    ScheduleChanged = 3,

    /// <summary>The record moved along its lifecycle: proposed to active, planned to released, withdrawn.</summary>
    StatusChanged = 4,

    /// <summary>
    /// The record was put into or taken out of use, keeping its place in its lifecycle: activated,
    /// deactivated, published, archived, retired.
    /// </summary>
    StateChanged = 5,

    /// <summary>A health signal was recorded, changed or withdrawn.</summary>
    Health = 6,

    /// <summary>The record stopped existing.</summary>
    Removed = 7,
}
