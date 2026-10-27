using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A tag on an intake request tag axis was taken out of use. Requests already carrying it keep it.
/// </summary>
public sealed record IntakeRequestTagDeactivatedEvent : DomainEvent<IntakeRequestTagDeactivatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public IntakeRequestTagDeactivatedEvent(Guid id, Guid tagId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TagId = tagId;

        Timestamp = timestamp;
    }

    /// <summary>The tag's axis.</summary>
    public Guid Id { get; }
    public Guid TagId { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
