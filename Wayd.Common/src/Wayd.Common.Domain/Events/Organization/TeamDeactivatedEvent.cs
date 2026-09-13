using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamDeactivatedEvent : DomainEvent<TeamDeactivatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public TeamDeactivatedEvent(Guid id, int key, TeamCode? code, LocalDate inactiveDate, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        Code = code;
        InactiveDate = inactiveDate;
        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>Added in 1.0 -> 1.1. Zero on a payload written before 1.1, which did not record it.</summary>
    public int Key { get; }

    /// <summary>Added in 1.0 -> 1.1. Null on a payload written before 1.1, which did not record it.</summary>
    public TeamCode? Code { get; }

    public LocalDate InactiveDate { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
