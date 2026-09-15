using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// An output's formula was changed.
/// </summary>
public sealed record ScoringModelOutputFormulaChangedEvent : DomainEvent<ScoringModelOutputFormulaChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ScoringModelOutputFormulaChangedEvent(
        Guid id,
        int key,
        Guid outputId,
        string previousFormula,
        string formula,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OutputId = outputId;
        PreviousFormula = previousFormula;
        Formula = formula;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid OutputId { get; }
    public string PreviousFormula { get; }
    public string Formula { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
