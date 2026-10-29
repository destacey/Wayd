using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake desk turned free tags on or off: whether its agents may give its requests tags on no axis, typed in
/// while tagging.
/// </summary>
public sealed record IntakeDeskFreeTagsChangedEvent : DomainEvent<IntakeDeskFreeTagsChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskFreeTagsChangedEvent(Guid id, int key, bool allowsFreeTags, bool previousAllowsFreeTags, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        AllowsFreeTags = allowsFreeTags;
        PreviousAllowsFreeTags = previousAllowsFreeTags;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>Whether the desk's agents may now add free tags.</summary>
    public bool AllowsFreeTags { get; }

    /// <summary>Whether they could before the change.</summary>
    public bool PreviousAllowsFreeTags { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
