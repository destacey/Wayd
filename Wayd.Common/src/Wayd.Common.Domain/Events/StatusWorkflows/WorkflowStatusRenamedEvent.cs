using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A status's name or description was edited.
/// </summary>
/// <remarks>
/// Allowed on a published workflow, unlike every other status change, because records hold the status id:
/// what they display changes, and what they mean does not.
/// </remarks>
public sealed record WorkflowStatusRenamedEvent : DomainEvent<WorkflowStatusRenamedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowStatusRenamedEvent(
        Guid id,
        int key,
        Guid statusId,
        string name,
        string? description,
        WorkflowStatusDetails previous,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        StatusId = statusId;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid StatusId { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The name and description this edit replaced.</summary>
    public WorkflowStatusDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
