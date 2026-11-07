using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project deliverable's name, description, acceptance criteria, owner or target date changed.
/// </summary>
public sealed record ProjectDeliverableDetailsUpdatedEvent : DomainEvent<ProjectDeliverableDetailsUpdatedEvent>, IDomainEventDescriptor, IPpmEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ProjectDeliverableDetailsUpdatedEvent(
        Guid id,
        ProjectKey key,
        Guid deliverableId,
        ProjectDeliverableDetails details,
        ProjectDeliverableDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        DeliverableId = deliverableId;
        Details = details;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }

    /// <summary>The deliverable this event is about.</summary>
    public Guid DeliverableId { get; }

    /// <summary>The details after the edit.</summary>
    public ProjectDeliverableDetails Details { get; }

    /// <summary>The details the edit replaced.</summary>
    public ProjectDeliverableDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
