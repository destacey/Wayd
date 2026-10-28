using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A tag on an intake request tag axis was renamed or its description changed. Every request carrying it reads
/// the new name.
/// </summary>
public sealed record IntakeRequestTagDetailsUpdatedEvent : DomainEvent<IntakeRequestTagDetailsUpdatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeRequestTagDetailsUpdatedEvent(Guid id, Guid tagId, string name, string? description, string previousName, string? previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TagId = tagId;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    /// <summary>The tag's axis.</summary>
    public Guid Id { get; }
    public Guid TagId { get; }
    public string Name { get; }
    public string? Description { get; }
    public string PreviousName { get; }
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
