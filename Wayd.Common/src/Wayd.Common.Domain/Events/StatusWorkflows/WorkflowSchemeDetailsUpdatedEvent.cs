using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow scheme's name or description was edited.
/// </summary>
public sealed record WorkflowSchemeDetailsUpdatedEvent : DomainEvent<WorkflowSchemeDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowSchemeDetailsUpdatedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        WorkflowSchemeDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The details this edit replaced.</summary>
    public WorkflowSchemeDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "WorkflowScheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
