using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// Tracking began for a status workflow that existed before <see cref="WorkflowCreatedEvent"/> was recorded.
/// Carries that event's payload, describing the workflow as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
/// <remarks>
/// <see cref="SourceWorkflowId"/> is recorded only by the creation event; a workflow does not keep it, so a
/// baseline written from the stored workflow has none.
/// </remarks>
public sealed record WorkflowBaselinedEvent : BaselineEvent<WorkflowBaselinedEvent, WorkflowCreatedEvent>
{
    public WorkflowBaselinedEvent(Guid id, int key, string name, string? description, string ownerType, bool isSystem, Guid? sourceWorkflowId, WorkflowStatusValues[] statuses, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("Workflow", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        OwnerType = ownerType;
        IsSystem = isSystem;
        SourceWorkflowId = sourceWorkflowId;
        Statuses = [.. statuses];
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string? Description { get; }
    public string OwnerType { get; }
    public bool IsSystem { get; }
    public Guid? SourceWorkflowId { get; }
    public WorkflowStatusValues[] Statuses { get; }
}
