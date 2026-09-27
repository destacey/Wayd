using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// One of a team of teams' operating models was corrected in place, so its new settings apply to its whole period.
/// A team of teams that moves from a date sets a new model instead.
/// </summary>
public sealed record TeamOfTeamsOperatingModelCorrectedEvent : DomainEvent<TeamOfTeamsOperatingModelCorrectedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamOfTeamsOperatingModelCorrectedEvent(Guid id, int key, FlexibleDateRange period, TeamOfTeamsOperatingModelSettings settings, TeamOfTeamsOperatingModelSettings previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Period = period;
        Settings = settings;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The days the corrected model applies to, which identify it.</summary>
    public FlexibleDateRange Period { get; }

    public TeamOfTeamsOperatingModelSettings Settings { get; }
    public TeamOfTeamsOperatingModelSettings Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
