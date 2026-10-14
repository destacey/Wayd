using NodaTime;

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
public sealed record GetTeamsCapacityByPeriodQuery(IReadOnlyCollection<Guid> TeamIds, IReadOnlyList<CapacityPeriod> Periods)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<TeamPeriodCapacity>>?>;

/// <summary>A run of days a capacity is asked for, both inclusive.</summary>
public sealed record CapacityPeriod(LocalDate Start, LocalDate End);

/// <summary>A team's capacity over one period, in person-days: one person allocated in full, and there, for one working day.</summary>
/// <param name="Period">The period, as asked for.</param>
/// <param name="AllocatedPersonDays">The time allocated in every role.</param>
/// <param name="DeliveryPersonDays">The part of it in roles that count toward delivery capacity.</param>
/// <param name="AwayPersonDays">The allocated time that people's time away takes, already left out of the other two.</param>
public sealed record TeamPeriodCapacity(CapacityPeriod Period, decimal AllocatedPersonDays, decimal DeliveryPersonDays, decimal AwayPersonDays);
