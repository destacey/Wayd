using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A managed workflow's source dropped one of its statuses, so the status was retired: records can no longer be given it, and those that hold it keep resolving.
/// </summary>
public sealed record WorkflowStatusRetiredEvent : DomainEvent<WorkflowStatusRetiredEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public WorkflowStatusRetiredEvent(
        Guid id,
        int key,
        Guid statusId,
        string externalKey,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        StatusId = statusId;
        ExternalKey = externalKey;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid StatusId { get; }

    /// <summary>The source's identifier for the status.</summary>
    public string ExternalKey { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
