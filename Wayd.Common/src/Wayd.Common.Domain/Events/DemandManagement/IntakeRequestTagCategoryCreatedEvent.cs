using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A tag category was added to the ones reviewers group intake requests by.
/// </summary>
public sealed record IntakeRequestTagCategoryCreatedEvent : DomainEvent<IntakeRequestTagCategoryCreatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public IntakeRequestTagCategoryCreatedEvent(Guid id, string name, string? description, bool allowsMany, int order, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        AllowsMany = allowsMany;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }

    /// <summary>What the category is for, when described.</summary>
    public string? Description { get; }

    /// <summary>Whether a request can carry several tags from the category.</summary>
    public bool AllowsMany { get; }

    /// <summary>Position when presenting the categories, lowest first.</summary>
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
