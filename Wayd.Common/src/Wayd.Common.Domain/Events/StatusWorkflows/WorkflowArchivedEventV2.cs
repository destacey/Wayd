using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow was withdrawn and can no longer be assigned.
/// </summary>
/// <remarks>
/// Only reachable once nothing assigns it, so this reports a workflow leaving the assignable set — not
/// records losing their statuses. Those keep resolving through it permanently, which is why archiving
/// retains the row rather than deleting it.
/// <para>
/// Supersedes <see cref="WorkflowArchivedEvent"/>, dropping its required <c>Name</c>, which described the
/// workflow rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record WorkflowArchivedEventV2 : DomainEvent, IAggregateEvent
{
    [JsonConstructor]
    public WorkflowArchivedEventV2(Guid id, int key, string ownerType, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        OwnerType = ownerType;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string OwnerType { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
