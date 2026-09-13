using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow was created as a draft, empty or as a copy of another.
/// </summary>
/// <remarks>
/// A clone is a creation rather than a separate occurrence: the new workflow comes into existence either
/// way, and <see cref="SourceWorkflowId"/> says where its statuses came from.
/// </remarks>
public sealed record WorkflowCreatedEvent : DomainEvent<WorkflowCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public WorkflowCreatedEvent(
        Guid id,
        int key,
        string name,
        string? description,
        string ownerType,
        bool isSystem,
        Guid? sourceWorkflowId,
        WorkflowStatusValues[] statuses,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        OwnerType = ownerType;
        IsSystem = isSystem;
        SourceWorkflowId = sourceWorkflowId;
        Statuses = [.. statuses];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The kind of record it governs, fixed for the workflow's life.</summary>
    public string OwnerType { get; }

    public bool IsSystem { get; }

    /// <summary>The workflow this one was cloned from, or null when it was created empty.</summary>
    public Guid? SourceWorkflowId { get; }

    /// <summary>The statuses it was created with, in display order.</summary>
    public WorkflowStatusValues[] Statuses { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
