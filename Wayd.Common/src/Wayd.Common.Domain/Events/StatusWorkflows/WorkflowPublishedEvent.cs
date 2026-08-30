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
/// </remarks>
public sealed record WorkflowPublishedEvent : DomainEvent
{
    public WorkflowPublishedEvent(Guid id, int key, string name, string ownerType, int statusCount, EventActor actor, Instant timestamp)
        : base(actor)
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

    /// <summary>Its name at the time, so a notification renders without a query.</summary>
    public string Name { get; }

    /// <summary>The kind of record it governs.</summary>
    public string OwnerType { get; }

    /// <summary>How many statuses it carries, for a notification that summarises without a query.</summary>
    public int StatusCount { get; }
}
