using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A draft workflow switched between allowing any move and allowing only its declared transitions.
/// </summary>
public sealed record WorkflowTransitionModeChangedEvent : DomainEvent<WorkflowTransitionModeChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowTransitionModeChangedEvent(
        Guid id,
        int key,
        TransitionMode fromMode,
        TransitionMode toMode,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromMode = fromMode;
        ToMode = toMode;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The mode before the change.</summary>
    public TransitionMode FromMode { get; }

    /// <summary>The mode after the change.</summary>
    public TransitionMode ToMode { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
