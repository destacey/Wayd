using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request tag axis no request was tagged along was deleted, with its tags.
/// </summary>
public sealed record IntakeRequestTagCategoryDeletedEvent : DomainEvent<IntakeRequestTagCategoryDeletedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public IntakeRequestTagCategoryDeletedEvent(Guid id, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
