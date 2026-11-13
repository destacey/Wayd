using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was estimated, its estimate changed, or its estimate cleared.
/// </summary>
/// <remarks>
/// One event rather than a type per verb: setting, changing and clearing all leave the request in one state.
/// Each end carries its scale version, so the entry reads the same after the desk moves to another scale.
/// <para>
/// Not a requester notification: the estimate is a sizing aid for the desk, and telling the requester each time
/// it changes would present it as a commitment.
/// </para>
/// <para>
/// Version 1.1 can carry <see cref="IntakeRequestEstimateSource.ScaleChange"/> as its <see cref="Source"/>.
/// </para>
/// </remarks>
public sealed record IntakeRequestEstimateChangedEvent : DomainEvent<IntakeRequestEstimateChangedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Substance;

    [JsonConstructor]
    public IntakeRequestEstimateChangedEvent(
        Guid id,
        int key,
        IntakeRequestEstimateDetails? from,
        IntakeRequestEstimateDetails? to,
        IntakeRequestEstimateSource source,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        From = from;
        To = to;
        Source = source;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The estimate before the change, or null when the request had none.</summary>
    public IntakeRequestEstimateDetails? From { get; }

    /// <summary>The estimate after the change, or null when it was cleared.</summary>
    public IntakeRequestEstimateDetails? To { get; }

    /// <summary>How the estimate came to change.</summary>
    public IntakeRequestEstimateSource Source { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
