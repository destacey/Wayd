using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A deactivated tag on an intake request tag axis was put back into use.
/// </summary>
public sealed record IntakeRequestTagActivatedEvent : DomainEvent<IntakeRequestTagActivatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public IntakeRequestTagActivatedEvent(Guid id, Guid tagId, EventActor actor, Instant timestamp)
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
