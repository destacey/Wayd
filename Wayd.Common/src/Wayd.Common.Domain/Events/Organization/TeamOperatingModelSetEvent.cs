using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A team adopted a new operating model from a date, ending the one it replaced the day before.
/// </summary>
public sealed record TeamOperatingModelSetEvent : DomainEvent<TeamOperatingModelSetEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamOperatingModelSetEvent(Guid id, int key, FlexibleDateRange period, TeamOperatingModelSettings settings, FlexibleDateRange? supersededPeriod, EventActor actor, Instant timestamp)
        : base(actor, "1.2")
    {
        Id = id;
        Key = key;
        Period = period;
        Settings = settings;
        SupersededPeriod = supersededPeriod;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>
    /// The days the new model applies to: open-ended, since it is now current. Operating models never
    /// overlap, so its start identifies it.
    /// </summary>
    public FlexibleDateRange Period { get; }

    public TeamOperatingModelSettings Settings { get; }

    /// <summary>
    /// The model this one replaced, as ended by it, or <c>null</c> for a team's first model. Only the
    /// current model is replaced, so until now the period was open-ended.
    /// </summary>
    public FlexibleDateRange? SupersededPeriod { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
