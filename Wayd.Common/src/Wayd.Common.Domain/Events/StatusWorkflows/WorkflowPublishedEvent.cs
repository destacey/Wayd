using System.Text.Json.Serialization;
using Wayd.Common.Domain.Events;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow became available to assign.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="WorkflowPublishedEventV2"/> replaced it. Kept
/// so every payload written as this type still deserializes into it — its name and members are the contract
/// those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by WorkflowPublishedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record WorkflowPublishedEvent : DomainEvent, IAggregateEvent
{
    [JsonConstructor]
    public WorkflowPublishedEvent(Guid id, int key, string name, string ownerType, int statusCount, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        OwnerType = ownerType;
        StatusCount = statusCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string OwnerType { get; }
    public int StatusCount { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
