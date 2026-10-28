using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was scored against its desk's scoring model.
/// </summary>
/// <remarks>
/// A score is an immutable snapshot, never edited, so recording one always adds to a series: the request's
/// current score is the one with the highest <see cref="Sequence"/>. The model is named as well as identified
/// because the score outlives the model that produced it.
/// <para>
/// Substance tier: a number that ranks one ask above another is what a restricted desk restricts.
/// </para>
/// <para>
/// <see cref="DomainEvent.EventId"/> is the id of the score the event records, so the two never describe the
/// same occurrence twice.
/// </para>
/// </remarks>
public sealed record IntakeRequestScoreRecordedEvent : DomainEvent<IntakeRequestScoreRecordedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Substance;

    [JsonConstructor]
    public IntakeRequestScoreRecordedEvent(
        // Named for the EventId property it binds to; any other name fails deserialization. The value is the
        // score's id.
        Guid eventId,
        Guid id,
        int key,
        Guid scoringModelId,
        string scoringModelName,
        decimal primaryValue,
        long sequence,
        Guid scoredById,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        EventId = eventId;

        Id = id;
        Key = key;
        ScoringModelId = scoringModelId;
        ScoringModelName = scoringModelName;
        PrimaryValue = primaryValue;
        Sequence = sequence;
        ScoredById = scoredById;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    public Guid ScoringModelId { get; }
    public string ScoringModelName { get; }

    /// <summary>The headline number the model produced, which is what ranking reads.</summary>
    public decimal PrimaryValue { get; }

    /// <summary>The request's monotonic score number, ordering the series.</summary>
    public long Sequence { get; }

    /// <summary>The employee who rated the request.</summary>
    public Guid ScoredById { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
