using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request's description or expected benefits changed.
/// </summary>
public sealed record IntakeRequestDetailsUpdatedEvent : DomainEvent<IntakeRequestDetailsUpdatedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Substance;

    public IntakeRequestDetailsUpdatedEvent(Guid id, int key, string description, string? expectedBenefits, string previousDescription, string? previousExpectedBenefits, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Description = description;
        ExpectedBenefits = expectedBenefits;
        PreviousDescription = previousDescription;
        PreviousExpectedBenefits = previousExpectedBenefits;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Description { get; }
    public string? ExpectedBenefits { get; }
    public string PreviousDescription { get; }
    public string? PreviousExpectedBenefits { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
