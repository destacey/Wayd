using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// Records whose type has no entry in a scheme now follow a different workflow, in every scope on it.
/// </summary>
public sealed record WorkflowSchemeDefaultChangedEvent : DomainEvent<WorkflowSchemeDefaultChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowSchemeDefaultChangedEvent(
        Guid id,
        int key,
        Guid fromWorkflowId,
        Guid toWorkflowId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromWorkflowId = fromWorkflowId;
        ToWorkflowId = toWorkflowId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The previous default workflow.</summary>
    public Guid FromWorkflowId { get; }

    /// <summary>The default workflow now.</summary>
    public Guid ToWorkflowId { get; }

    [JsonIgnore]
    public string AggregateType => "WorkflowScheme";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
