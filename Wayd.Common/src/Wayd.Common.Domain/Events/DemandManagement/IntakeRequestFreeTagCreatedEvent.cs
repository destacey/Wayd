using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A free intake request tag — one on no axis — was created by someone typing its name while tagging.
/// </summary>
public sealed record IntakeRequestFreeTagCreatedEvent : DomainEvent<IntakeRequestFreeTagCreatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public IntakeRequestFreeTagCreatedEvent(Guid id, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;

        Timestamp = timestamp;
    }

    /// <summary>The free tag.</summary>
    public Guid Id { get; }

    /// <summary>The tag's name, as first typed.</summary>
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTag";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
