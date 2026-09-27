using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A team of teams' current operating model was removed, and the one before it became current again.
/// </summary>
public sealed record TeamOfTeamsOperatingModelRemovedEvent : DomainEvent<TeamOfTeamsOperatingModelRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamOfTeamsOperatingModelRemovedEvent(Guid id, int key, FlexibleDateRange period, TeamOfTeamsOperatingModelSettings settings, FlexibleDateRange reinstatedPeriod, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Period = period;
        Settings = settings;
        ReinstatedPeriod = reinstatedPeriod;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The days the removed model applied to.</summary>
    public FlexibleDateRange Period { get; }

    /// <summary>How the team of teams worked under the removed model.</summary>
    public TeamOfTeamsOperatingModelSettings Settings { get; }

    /// <summary>
    /// The model that is current again, now open-ended. Until now it ended the day before
    /// <see cref="Period"/> began.
    /// </summary>
    public FlexibleDateRange ReinstatedPeriod { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
