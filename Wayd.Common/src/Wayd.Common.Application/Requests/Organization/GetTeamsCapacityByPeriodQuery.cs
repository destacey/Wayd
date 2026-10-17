using NodaTime;
using Wayd.Common.Domain.Models.Planning.Iterations;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// The capacity of each of <paramref name="TeamIds"/> over each of <paramref name="Periods"/>, by team id: the
/// time allocated to it in person-days, after the holidays each person takes and their time away. A team of
/// teams has its own allocations and those of the teams beneath it for the days they were in its hierarchy, as
/// its rollup counts them.
/// </summary>
/// <remarks>
/// Null when the Team Capacity Management feature is off. A team id that names no team has no entry. The same
/// handful of reads answers however many teams and periods are asked for, so a caller asks once for all of them.
/// </remarks>
/// <param name="TeamDaysOff">
/// Days each team is off for some or all of beyond its working week and holidays, by team id, such as its sprints'
/// team days off. Unlike <see cref="CapacityPeriod.DaysOff"/> they apply wherever the team is counted, including
/// beneath a team of teams. A team left out has none.
/// </param>
public sealed record GetTeamsCapacityByPeriodQuery(
    IReadOnlyCollection<Guid> TeamIds,
    IReadOnlyList<CapacityPeriod> Periods,
    IReadOnlyDictionary<Guid, IReadOnlyList<TeamDayOff>>? TeamDaysOff = null)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<TeamPeriodCapacity>>?>;

/// <summary>A run of days a capacity is asked for, both inclusive.</summary>
/// <param name="DaysOff">
/// Days the team asked for is off for some or all of in the period beyond its working week and holidays, such as a
/// sprint's team days off. Each takes its share of everyone's time on that team; the teams beneath a team of teams
/// are not affected.
/// </param>
public sealed record CapacityPeriod(LocalDate Start, LocalDate End, IReadOnlyCollection<TeamDayOff>? DaysOff = null)
{
    /// <summary>Compares the days off by value, so a period equals one asked for with the same days.</summary>
    public bool Equals(CapacityPeriod? other) =>
        other is not null
        && Start == other.Start
        && End == other.End
        && (DaysOff ?? []).OrderBy(d => d.Date).SequenceEqual((other.DaysOff ?? []).OrderBy(d => d.Date));

    public override int GetHashCode() => HashCode.Combine(Start, End, (DaysOff ?? []).Count);
}

/// <summary>A team's capacity over one period, in person-days: one person allocated in full, and there, for one working day.</summary>
/// <param name="Period">The period, as asked for.</param>
/// <param name="AllocatedPersonDays">The time allocated in every role.</param>
/// <param name="DeliveryPersonDays">The part of it in roles that count toward delivery capacity.</param>
/// <param name="AwayPersonDays">The allocated time that people's time away takes, already left out of the other two.</param>
/// <param name="HasAllocations">
/// Whether anyone is allocated to the team, or to a team beneath a team of teams while it was there, on any day of
/// the period, whatever holidays, time away and days off leave of it. False is a team with nobody to plan with,
/// as opposed to one whose people are all away.
/// </param>
public sealed record TeamPeriodCapacity(CapacityPeriod Period, decimal AllocatedPersonDays, decimal DeliveryPersonDays, decimal AwayPersonDays, bool HasAllocations = false);
