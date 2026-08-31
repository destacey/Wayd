using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow was withdrawn and can no longer be assigned.
/// </summary>
/// <remarks>
/// Only reachable once nothing assigns it, so this reports a workflow leaving the assignable set — not
/// records losing their statuses. Those keep resolving through it permanently, which is why archiving
/// retains the row rather than deleting it.
/// </remarks>
public sealed record WorkflowArchivedEvent : DomainEvent
{
    public WorkflowArchivedEvent(Guid id, int key, string name, string ownerType, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;
        Key = key;
        Name = name;
        OwnerType = ownerType;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string OwnerType { get; }
}
