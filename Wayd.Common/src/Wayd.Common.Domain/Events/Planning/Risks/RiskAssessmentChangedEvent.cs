using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk's impact or likelihood was reassessed.
/// </summary>
/// <remarks>
/// Exposure is derived from the two grades, and is carried at both ends so a consumer reacting to a risk becoming
/// high exposure does not have to know the rule that derives it.
/// </remarks>
public sealed record RiskAssessmentChangedEvent : DomainEvent<RiskAssessmentChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RiskAssessmentChangedEvent(
        Guid id,
        int key,
        RiskGrade previousImpact,
        RiskGrade previousLikelihood,
        RiskGrade previousExposure,
        RiskGrade impact,
        RiskGrade likelihood,
        RiskGrade exposure,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousImpact = previousImpact;
        PreviousLikelihood = previousLikelihood;
        PreviousExposure = previousExposure;
        Impact = impact;
        Likelihood = likelihood;
        Exposure = exposure;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public RiskGrade PreviousImpact { get; }
    public RiskGrade PreviousLikelihood { get; }
    public RiskGrade PreviousExposure { get; }
    public RiskGrade Impact { get; }
    public RiskGrade Likelihood { get; }
    public RiskGrade Exposure { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
