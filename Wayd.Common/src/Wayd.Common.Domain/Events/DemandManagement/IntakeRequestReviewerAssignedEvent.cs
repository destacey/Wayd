using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request's assigned reviewer changed, or it was assigned or unassigned.
/// </summary>
/// <remarks>
/// Plain ownership, never an approver.
/// </remarks>
public sealed record IntakeRequestReviewerAssignedEvent : DomainEvent<IntakeRequestReviewerAssignedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestReviewerAssignedEvent(Guid id, int key, Guid? fromReviewerId, Guid? toReviewerId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromReviewerId = fromReviewerId;
        ToReviewerId = toReviewerId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    /// <summary>The employee assigned before, or null when nobody was.</summary>
    public Guid? FromReviewerId { get; }
    /// <summary>The employee assigned now, or null when it was unassigned.</summary>
    public Guid? ToReviewerId { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
