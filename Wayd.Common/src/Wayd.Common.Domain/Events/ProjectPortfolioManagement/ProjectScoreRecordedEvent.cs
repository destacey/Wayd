using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

/// <summary>
/// A project was scored against its portfolio's scoring model.
/// </summary>
/// <remarks>
/// A score is an immutable snapshot taken at a point in time, never edited, so recording one is always an
/// addition to a series. The model is named as well as identified because a score outlives the model
/// version that produced it, and a reader needs to know what it was scored against.
/// <para>
/// Appended rather than superseding, for the same reason as a health check: each score is its own record,
/// and <see cref="Sequence"/> is what orders the series.
/// </para>
/// </remarks>
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
        : base(actor)
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
