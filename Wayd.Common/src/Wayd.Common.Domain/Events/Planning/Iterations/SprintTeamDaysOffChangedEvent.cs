using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team changed the days within the sprint that the whole team is off, such as an offsite. The sprint's
/// ideal burn-down stays flat on them.
/// </summary>
public sealed record SprintTeamDaysOffChangedEvent : DomainEvent<SprintTeamDaysOffChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public SprintTeamDaysOffChangedEvent(Guid id, int key, IReadOnlyList<LocalDate> added, IReadOnlyList<LocalDate> removed, IReadOnlyList<LocalDate> teamDaysOff, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = added;
        Removed = removed;
        TeamDaysOff = teamDaysOff;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public IReadOnlyList<LocalDate> Added { get; }
    public IReadOnlyList<LocalDate> Removed { get; }

    /// <summary>The sprint's team days off after the change, in date order.</summary>
    public IReadOnlyList<LocalDate> TeamDaysOff { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
