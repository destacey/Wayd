using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// One of a team's operating models was corrected in place, so its new settings apply to its whole period.
/// A team that changes how it works from a date sets a new model instead.
/// </summary>
public sealed record TeamOperatingModelCorrectedEvent : DomainEvent<TeamOperatingModelCorrectedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamOperatingModelCorrectedEvent(Guid id, int key, FlexibleDateRange period, TeamOperatingModelSettings settings, TeamOperatingModelSettings previous, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
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

    public TeamOperatingModelSettings Settings { get; }
    public TeamOperatingModelSettings Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
