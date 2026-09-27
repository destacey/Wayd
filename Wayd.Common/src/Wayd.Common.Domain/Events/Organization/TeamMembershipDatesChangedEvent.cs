using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// The period of a team's or team of teams' membership in a parent team of teams moved.
/// </summary>
public sealed record TeamMembershipDatesChangedEvent : DomainEvent<TeamMembershipDatesChangedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public TeamMembershipDatesChangedEvent(Guid id, int key, Guid parentTeamId, FlexibleDateRange dateRange, FlexibleDateRange previousDateRange, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ParentTeamId = parentTeamId;
        DateRange = dateRange;
        PreviousDateRange = previousDateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ParentTeamId { get; }
    public FlexibleDateRange DateRange { get; }
    public FlexibleDateRange PreviousDateRange { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;

    /// <summary>The parent team of teams, whose Activity shows its child's membership moving.</summary>
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(AggregateType, ParentTeamId)];
}
