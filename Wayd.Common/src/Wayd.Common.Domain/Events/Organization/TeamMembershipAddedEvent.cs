using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A team or team of teams became a member of a parent team of teams for a period.
/// </summary>
public sealed record TeamMembershipAddedEvent : DomainEvent<TeamMembershipAddedEvent>, IDomainEventDescriptor, IRelatedAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamMembershipAddedEvent(Guid id, int key, Guid parentTeamId, FlexibleDateRange dateRange, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ParentTeamId = parentTeamId;
        DateRange = dateRange;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ParentTeamId { get; }

    /// <summary>
    /// The days the membership holds. A team has one parent at a time, so together with
    /// <see cref="ParentTeamId"/> it identifies the membership.
    /// </summary>
    public FlexibleDateRange DateRange { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;

    /// <summary>The parent team of teams, whose Activity shows it gaining a child.</summary>
    [JsonIgnore]
    public IReadOnlyCollection<AggregateReference> RelatedAggregates => [new(AggregateType, ParentTeamId)];
}
