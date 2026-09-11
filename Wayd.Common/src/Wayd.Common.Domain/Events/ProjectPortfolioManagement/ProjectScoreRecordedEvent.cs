using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project was scored against its portfolio's scoring model.
/// </summary>
/// <remarks>
/// Frozen at its published shape and never raised; <see cref="ProjectScoreRecordedEventV2"/> replaced it.
/// Kept so every payload written as this type still deserializes into it — its name and members are the
/// contract those payloads were written against, so neither may change.
/// </remarks>
[Obsolete("Superseded by ProjectScoreRecordedEventV2. Kept only to deserialize payloads already written as this type.")]
public sealed record ProjectScoreRecordedEvent : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectScoreRecordedEvent(
        Guid id,
        ProjectKey key,
        string name,
        Guid scoreId,
        Guid scoringModelId,
        string scoringModelName,
        decimal primaryValue,
        long sequence,
        Guid scoredById,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        ScoreId = scoreId;
        ScoringModelId = scoringModelId;
        ScoringModelName = scoringModelName;
        PrimaryValue = primaryValue;
        Sequence = sequence;
        ScoredById = scoredById;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ProjectKey Key { get; }
    public string Name { get; }

    /// <summary>The score this event is about.</summary>
    public Guid ScoreId { get; }

    public Guid ScoringModelId { get; }
    public string ScoringModelName { get; }

    /// <summary>The headline number the model produced, which is what ranking reads.</summary>
    public decimal PrimaryValue { get; }

    /// <summary>The project's monotonic score number, ordering the series.</summary>
    public long Sequence { get; }

    public Guid ScoredById { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
