using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow became available to assign.
/// </summary>
/// <remarks>
/// The end of the build-and-review flow: someone drafted it, others looked at it, and this is the
/// moment it can be put into use. Whoever asked for it is waiting on exactly this.
/// <para>
/// Says nothing about anything using it — several workflows for one owner type are published at once
/// by design, each scope picking its own. <c>WorkflowAssignedEvent</c> is what reports use.
/// </para>
/// <para>
/// Supersedes <see cref="WorkflowPublishedEvent"/>, dropping its required <c>Name</c>, which described the
/// workflow rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record WorkflowPublishedEventV2 : DomainEvent, IAggregateEvent
{
    [JsonConstructor]
    public WorkflowPublishedEventV2(Guid id, int key, string ownerType, int statusCount, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        OwnerType = ownerType;
        StatusCount = statusCount;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The kind of record it governs.</summary>
    public string OwnerType { get; }

    /// <summary>How many statuses it was published with.</summary>
    public int StatusCount { get; }

    [JsonIgnore]
    public string AggregateType => "Workflow";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
