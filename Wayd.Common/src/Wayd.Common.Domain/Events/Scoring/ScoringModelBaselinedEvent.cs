using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// Tracking began for a scoring model that existed before <see cref="ScoringModelCreatedEvent"/> was recorded.
/// Carries that event's payload, describing the model as it stood at <see cref="DomainEvent.Timestamp"/>.
/// </summary>
public sealed record ScoringModelBaselinedEvent : BaselineEvent<ScoringModelBaselinedEvent, ScoringModelCreatedEvent>
{
    public ScoringModelBaselinedEvent(Guid id, int key, string name, string description, ScoringScaleValues[] scales, ScoringCriterionValues[] criteria, ScoringOutputValues[] outputs, Instant? recordCreatedOn, Guid? recordCreatedById, Instant timestamp)
        : base("ScoringModel", id, recordCreatedOn, recordCreatedById, timestamp, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Scales = [.. scales];
        Criteria = [.. criteria];
        Outputs = [.. outputs];
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
    public ScoringScaleValues[] Scales { get; }
    public ScoringCriterionValues[] Criteria { get; }
    public ScoringOutputValues[] Outputs { get; }
}
