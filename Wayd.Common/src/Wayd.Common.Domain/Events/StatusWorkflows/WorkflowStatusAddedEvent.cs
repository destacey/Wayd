using System.Text.Json.Serialization;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A status was added to a workflow.
/// </summary>
public sealed record WorkflowStatusAddedEvent : DomainEvent<WorkflowStatusAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowStatusAddedEvent(
        Guid id,
        int key,
        Guid statusId,
        string name,
        string? description,
        StatusCategory category,
        int alias,
        int order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        StatusId = statusId;
        Name = name;
        Description = description;
        Category = category;
        Alias = alias;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid StatusId { get; }
    public string Name { get; }
    public string? Description { get; }
    public StatusCategory Category { get; }

    /// <summary>The well-known meaning, in the vocabulary of the workflow's owner type; 0 for none.</summary>
    public int Alias { get; }

    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
