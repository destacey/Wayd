using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was moved to another desk, and with it who owns the decision on it.
/// </summary>
/// <remarks>
/// Carries the visibility of both desks as they were at the move, because that is what decided who could read
/// the request before and after it, and a desk's visibility can change later.
/// </remarks>
public sealed record IntakeRequestReroutedEvent : DomainEvent<IntakeRequestReroutedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestReroutedEvent(Guid id, int key, Guid fromDeskId, Guid toDeskId, IntakeDeskVisibility fromVisibility,
        IntakeDeskVisibility toVisibility, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromDeskId = fromDeskId;
        ToDeskId = toDeskId;
        FromVisibility = fromVisibility;
        ToVisibility = toVisibility;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid FromDeskId { get; }
    public Guid ToDeskId { get; }

    /// <summary>The visibility of the desk the request left, as it was at the move.</summary>
    public IntakeDeskVisibility FromVisibility { get; }

    /// <summary>The visibility of the desk the request moved to, as it was at the move.</summary>
    public IntakeDeskVisibility ToVisibility { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
