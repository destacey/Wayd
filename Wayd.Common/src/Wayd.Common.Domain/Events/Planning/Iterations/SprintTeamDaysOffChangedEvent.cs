using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team changed the days within the sprint that the whole team is off for some or all of, such as an
/// offsite. The sprint's ideal burn-down drops that much less on them.
/// </summary>
/// <remarks>
/// 1.1 added the part days. A date listed with no part-day entry is a full day off, so a 1.0 payload, which
/// carries none, still reads as it was written.
/// </remarks>
public sealed record SprintTeamDaysOffChangedEvent : DomainEvent<SprintTeamDaysOffChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public SprintTeamDaysOffChangedEvent(
        Guid id,
        int key,
        IReadOnlyList<LocalDate> added,
        IReadOnlyList<LocalDate> removed,
        IReadOnlyList<LocalDate> teamDaysOff,
        EventActor actor,
        Instant timestamp,
        IReadOnlyList<TeamDayOff>? partDaysOff = null,
        IReadOnlyList<TeamDayOff>? previousPartDaysOff = null)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        Added = added;
        Removed = removed;
        TeamDaysOff = teamDaysOff;
        PartDaysOff = partDaysOff ?? [];
        PreviousPartDaysOff = previousPartDaysOff ?? [];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public IReadOnlyList<LocalDate> Added { get; }
    public IReadOnlyList<LocalDate> Removed { get; }

    /// <summary>The sprint's team days off after the change, in date order.</summary>
    public IReadOnlyList<LocalDate> TeamDaysOff { get; }

    /// <summary>
    /// The days of <see cref="TeamDaysOff"/> the team is off for only part of, with the percent off, in date
    /// order. Every other day listed is a full day off.
    /// </summary>
    public IReadOnlyList<TeamDayOff> PartDaysOff { get; }

    /// <summary>
    /// The part days before the change, so a day whose percent changed carries both ends. A day that was off
    /// before and is not listed here was a full day off.
    /// </summary>
    public IReadOnlyList<TeamDayOff> PreviousPartDaysOff { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
