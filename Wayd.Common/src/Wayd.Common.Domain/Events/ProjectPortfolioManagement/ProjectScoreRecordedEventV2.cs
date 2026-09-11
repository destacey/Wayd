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
/// Supersedes <see cref="ProjectScoreRecordedEvent"/>, dropping its required <c>Name</c>, which described the
/// project rather than the change. A new type rather than a new version, because removing a required member
/// breaks every consumer written against the old shape.
/// </para>
/// </remarks>
public sealed record ProjectScoreRecordedEventV2 : DomainEvent, IPpmEvent
{
    [JsonConstructor]
    public ProjectScoreRecordedEventV2(
        Guid id,
        ProjectKey key,
        Guid scoreId,
        Guid scoringModelId,
        string scoringModelName,
        decimal primaryValue,
        long sequence,
        Guid scoredById,
        EventActor actor,
        Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
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
