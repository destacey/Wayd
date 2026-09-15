using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Scoring;

/// <summary>
/// A scoring model was created as a proposed model, empty or with starting scales, criteria and outputs.
/// </summary>
/// <remarks>
/// Criteria reference their scale by id, so a consumer resolves it from <see cref="Scales"/> in the same payload.
/// </remarks>
public sealed record ScoringModelCreatedEvent : DomainEvent<ScoringModelCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public ScoringModelCreatedEvent(
        Guid id,
        int key,
        string name,
        string description,
        ScoringScaleValues[] scales,
        ScoringCriterionValues[] criteria,
        ScoringOutputValues[] outputs,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Scales = [.. scales];
        Criteria = [.. criteria];
        Outputs = [.. outputs];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>The scales it was created with, in display order.</summary>
    public ScoringScaleValues[] Scales { get; }

    /// <summary>The criteria it was created with, in display order.</summary>
    public ScoringCriterionValues[] Criteria { get; }

    /// <summary>The outputs it was created with, in evaluation order.</summary>
    public ScoringOutputValues[] Outputs { get; }

    [JsonIgnore]
    public string AggregateType => "ScoringModel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
