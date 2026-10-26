using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was given a different assignee, or was assigned or unassigned.
/// </summary>
/// <remarks>
/// Plain ownership, never an approver.
/// </remarks>
public sealed record IntakeRequestAssigneeChangedEvent : DomainEvent<IntakeRequestAssigneeChangedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestAssigneeChangedEvent(Guid id, int key, Guid? fromAssigneeId, Guid? toAssigneeId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromAssigneeId = fromAssigneeId;
        ToAssigneeId = toAssigneeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    /// <summary>The employee assigned before, or null when nobody was.</summary>
    public Guid? FromAssigneeId { get; }
    /// <summary>The employee assigned now, or null when it was unassigned.</summary>
    public Guid? ToAssigneeId { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
