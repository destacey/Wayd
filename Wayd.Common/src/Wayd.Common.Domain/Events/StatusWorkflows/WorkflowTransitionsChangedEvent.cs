using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// The moves a draft workflow allows, or the conditions on them, changed — by an edit, or because a status
/// they touched was removed.
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
        WorkflowTransitionConditionsChange[]? conditionsChanged,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        Transitions = [.. transitions];
        ConditionsChanged = [.. conditionsChanged ?? []];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The transitions the change added.</summary>
    public WorkflowTransitionValues[] Added { get; }

    /// <summary>The transitions the change removed.</summary>
    public WorkflowTransitionValues[] Removed { get; }

    /// <summary>Every transition the workflow allows after the change, with its conditions.</summary>
    public WorkflowTransitionValues[] Transitions { get; }

    /// <summary>
    /// The kept transitions whose conditions the change replaced, with both ends. Added in 1.1; empty in
    /// earlier payloads, which predate conditions.
    /// </summary>
    public WorkflowTransitionConditionsChange[] ConditionsChanged { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
