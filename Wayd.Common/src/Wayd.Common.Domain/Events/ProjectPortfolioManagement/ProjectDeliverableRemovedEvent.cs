using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A deliverable was deleted from a project before work on the project began. One dropped later is descoped
/// instead, which keeps it.
/// </summary>
public sealed record ProjectDeliverableRemovedEvent : DomainEvent<ProjectDeliverableRemovedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ProjectDeliverableRemovedEvent(
        Guid id,
        ProjectKey key,
        Guid deliverableId,
        string name,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        DeliverableId = deliverableId;
        Name = name;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The deliverable this event is about.</summary>
    public Guid DeliverableId { get; }

    /// <summary>What the deliverable was called, since nothing remains to read it from.</summary>
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
