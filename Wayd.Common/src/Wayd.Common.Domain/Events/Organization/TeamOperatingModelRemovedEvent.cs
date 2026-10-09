using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A team's current operating model was removed, and the one before it became current again.
/// </summary>
public sealed record TeamOperatingModelRemovedEvent : DomainEvent<TeamOperatingModelRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamOperatingModelRemovedEvent(Guid id, int key, FlexibleDateRange period, TeamOperatingModelSettings settings, FlexibleDateRange reinstatedPeriod, EventActor actor, Instant timestamp)
        : base(actor, "1.2")
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

    /// <summary>How the team worked under the removed model.</summary>
    public TeamOperatingModelSettings Settings { get; }

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
