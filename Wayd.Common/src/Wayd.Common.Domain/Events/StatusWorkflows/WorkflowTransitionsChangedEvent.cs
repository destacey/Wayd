using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// The moves a draft workflow allows changed, by an edit or because a status they touched was removed.
/// </summary>
public sealed record WorkflowTransitionsChangedEvent : DomainEvent<WorkflowTransitionsChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowTransitionsChangedEvent(
        Guid id,
        int key,
        WorkflowTransitionValues[] added,
        WorkflowTransitionValues[] removed,
        WorkflowTransitionValues[] transitions,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        Transitions = [.. transitions];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The transitions the change added.</summary>
    public WorkflowTransitionValues[] Added { get; }

    /// <summary>The transitions the change removed.</summary>
    public WorkflowTransitionValues[] Removed { get; }

    /// <summary>Every transition the workflow allows after the change.</summary>
    public WorkflowTransitionValues[] Transitions { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
