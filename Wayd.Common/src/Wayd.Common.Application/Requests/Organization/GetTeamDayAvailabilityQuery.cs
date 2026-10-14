using NodaTime;

namespace Wayd.Common.Application.Requests.Organization;

/// <summary>
/// How much of an Organization team's delivery capacity is there on each day from <paramref name="From"/> to
/// <paramref name="To"/>, both inclusive, by day: the people allocated to it in roles that count toward
/// delivery capacity, less the holidays of the calendar each of them follows and their time away. A day nobody
/// is allocated on has no entry.
/// </summary>
/// <remarks>
/// Null when the answer does not apply: the Team Capacity Management feature is off, or the team has no such
/// allocation in the range. The caller then counts the team's days as it did before allocations existed.
/// Neither the team's working week, its own holiday calendar nor a sprint's team days off is applied here: a
/// person's holidays come from the calendar assigned to them or the system default, and the rest are the caller's.
/// </remarks>
public sealed record GetTeamDayAvailabilityQuery(Guid TeamId, LocalDate From, LocalDate To)
    : IQuery<IReadOnlyDictionary<LocalDate, TeamDayAvailability>?>;

/// <summary>A team's delivery capacity on one day, in people: 1 is one person allocated in full.</summary>
/// <param name="Allocated">The time allocated to the team that day in roles that count toward delivery capacity. Above zero.</param>
/// <param name="Available">The part of it that is there, after holidays and time away.</param>
public sealed record TeamDayAvailability(decimal Allocated, decimal Available)
{
    /// <summary>The share of the team's allocated delivery time that is there, from 0 to 1.</summary>
    public decimal Share => Available / Allocated;
}
