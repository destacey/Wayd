using NodaTime;
using Wayd.Common.Application.Requests.Organization;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>An allocation as the capacity sums read it.</summary>
/// <param name="CountsTowardDeliveryCapacity">False when the allocation's role does not add to delivery capacity.</param>
/// <param name="End">The last day, or null when the allocation has no end.</param>
public sealed record CapacityAllocation(Guid EmployeeId, Guid TeamId, bool CountsTowardDeliveryCapacity, LocalDate Start, LocalDate? End, int Percent);

/// <summary>
/// One person's time over a team's working days, in person-days, after the holidays they take and their time away.
/// </summary>
/// <param name="AllocatedPersonDays">Their time allocated to the team, in every role, that they are there for.</param>
/// <param name="DeliveryPersonDays">The part of it in roles that count toward delivery capacity.</param>
/// <param name="ElsewherePersonDays">Their time allocated to other teams on the same days that they are there for.</param>
/// <param name="AwayPersonDays">The time allocated to the team that their time away takes.</param>
public sealed record PersonCapacity(decimal AllocatedPersonDays, decimal DeliveryPersonDays, decimal ElsewherePersonDays, decimal AwayPersonDays);

/// <summary>One working day of a person's time in one role on a team.</summary>
/// <param name="Percent">The share of the person's time allocated that day, from 1 to 100.</param>
/// <param name="PersonDays">What it is worth after the person's holidays and time away.</param>
public sealed record CapacityPlanDay(LocalDate Date, int Percent, decimal PersonDays);

/// <summary>What else decides a person's time on a team's working day, beside their allocation to it.</summary>
/// <param name="IsHoliday">Whether the day is a holiday in the calendar the person takes.</param>
/// <param name="PercentAway">The share of the day the person is away, from 0 to 100.</param>
/// <param name="ElsewherePercent">The share of the person's time allocated to other teams that day.</param>
public sealed record CapacityPlanPersonDay(LocalDate Date, bool IsHoliday, int PercentAway, int ElsewherePercent);

/// <summary>
/// Turns allocations into person-days: an allocation of 50% over ten working days the person is there for is
/// five. Pure: everything it needs is passed in.
/// </summary>
public static class TeamCapacityCalculator
{
    private const decimal Whole = 100m;

    /// <summary>
    /// Each person's capacity for <paramref name="teamId"/> over <paramref name="teamDays"/>, by employee id.
    /// Time on other teams is counted on this team's working days too, so the figures are comparable.
    /// </summary>
    public static IReadOnlyDictionary<Guid, PersonCapacity> Calculate(
        Guid teamId,
        IReadOnlyCollection<TeamDay> teamDays,
        IEnumerable<CapacityAllocation> allocations,
        AvailabilityCalculator availability)
    {
        Dictionary<Guid, PersonCapacity> people = [];

        foreach (var person in allocations.GroupBy(a => a.EmployeeId))
        {
            decimal allocated = 0, delivery = 0, elsewhere = 0, away = 0;

            foreach (var allocation in person)
            {
                var personDays = PersonDays(teamDays, person.Key, allocation.Start, allocation.End, allocation.Percent, availability);

                if (allocation.TeamId != teamId)
                {
                    elsewhere += personDays;
                    continue;
                }

                allocated += personDays;
                away += AwayPersonDays(teamDays, person.Key, allocation.Start, allocation.End, allocation.Percent, availability);
                if (allocation.CountsTowardDeliveryCapacity)
                    delivery += personDays;
            }

            people[person.Key] = new PersonCapacity(allocated, delivery, elsewhere, away);
        }

        return people;
    }

    /// <summary>
    /// The person-days one allocation is worth: its percent of each of the <paramref name="teamDays"/> it
    /// covers, for as much of that day as the person is there.
    /// </summary>
    /// <param name="end">The allocation's last day, or null when it has no end.</param>
    public static decimal PersonDays(IReadOnlyCollection<TeamDay> teamDays, Guid employeeId, LocalDate start, LocalDate? end, int percent, AvailabilityCalculator availability) =>
        Covered(teamDays, start, end).Sum(day => availability.Available(employeeId, day)) * percent / Whole;

    /// <summary>
    /// The person-days of one allocation that the person's time away takes: its percent of the share of each
    /// covered day they are away, of the share the team works. A holiday they take is not time away.
    /// </summary>
    /// <param name="end">The allocation's last day, or null when it has no end.</param>
    public static decimal AwayPersonDays(IReadOnlyCollection<TeamDay> teamDays, Guid employeeId, LocalDate start, LocalDate? end, int percent, AvailabilityCalculator availability) =>
        Covered(teamDays, start, end)
            .Where(day => !availability.IsHoliday(employeeId, day))
            .Sum(day => availability.ShareAway(employeeId, day.Date) * day.ShareWorked) * percent / Whole;

    /// <summary>
    /// How much of a team's delivery capacity is there on each of <paramref name="days"/>, by day: the time
    /// allocated in roles that count toward delivery capacity, and the part of it people are there for. A day
    /// with no such allocation has no entry, since a share of nothing is not a share.
    /// </summary>
    public static IReadOnlyDictionary<LocalDate, TeamDayAvailability> DeliveryAvailabilityByDay(
        IEnumerable<TeamDay> days,
        IReadOnlyCollection<CapacityAllocation> allocations,
        AvailabilityCalculator availability)
    {
        Dictionary<LocalDate, TeamDayAvailability> byDay = [];

        foreach (var day in days)
        {
            decimal allocated = 0, available = 0;

            foreach (var allocation in allocations)
            {
                if (!allocation.CountsTowardDeliveryCapacity || allocation.Start > day.Date || allocation.End < day.Date)
                    continue;

                allocated += allocation.Percent / Whole;
                available += allocation.Percent / Whole * availability.Available(allocation.EmployeeId, day);
            }

            if (allocated > 0)
                byDay[day.Date] = new TeamDayAvailability(allocated, available);
        }

        return byDay;
    }

    /// <summary>
    /// One person's time in one team member role, day by day: for each of <paramref name="teamDays"/> that
    /// <paramref name="allocations"/> cover, the percent allocated and the person-days it is worth. A day with
    /// nothing allocated has no entry.
    /// </summary>
    /// <param name="allocations">The person's allocations to the team in the one role, which never overlap.</param>
    public static IReadOnlyList<CapacityPlanDay> DailyAllocation(
        IReadOnlyCollection<TeamDay> teamDays,
        Guid employeeId,
        IReadOnlyCollection<CapacityAllocation> allocations,
        AvailabilityCalculator availability)
    {
        List<CapacityPlanDay> days = [];

        foreach (var day in teamDays)
        {
            var percent = allocations
                .Where(a => a.Start <= day.Date && (a.End is null || day.Date <= a.End))
                .Sum(a => a.Percent);

            if (percent > 0)
                days.Add(new CapacityPlanDay(day.Date, percent, percent / Whole * availability.Available(employeeId, day)));
        }

        return days;
    }

    /// <summary>
    /// What else decides one person's time on each of <paramref name="teamDays"/>: a holiday in the calendar
    /// they take, the share of the day they are away, and their time allocated to other teams. A day with none
    /// of these has no entry.
    /// </summary>
    /// <param name="elsewhere">The person's allocations to other teams.</param>
    public static IReadOnlyList<CapacityPlanPersonDay> DailyConditions(
        IReadOnlyCollection<TeamDay> teamDays,
        Guid employeeId,
        IReadOnlyCollection<CapacityAllocation> elsewhere,
        AvailabilityCalculator availability)
    {
        List<CapacityPlanPersonDay> days = [];

        foreach (var day in teamDays)
        {
            var isHoliday = availability.IsHoliday(employeeId, day);
            var percentAway = (int)Math.Round(availability.ShareAway(employeeId, day.Date) * Whole);
            var elsewherePercent = elsewhere
                .Where(a => a.Start <= day.Date && (a.End is null || day.Date <= a.End))
                .Sum(a => a.Percent);

            if (isHoliday || percentAway > 0 || elsewherePercent > 0)
                days.Add(new CapacityPlanPersonDay(day.Date, isHoliday, percentAway, elsewherePercent));
        }

        return days;
    }

    private static IEnumerable<TeamDay> Covered(IReadOnlyCollection<TeamDay> teamDays, LocalDate start, LocalDate? end) =>
        teamDays.Where(d => start <= d.Date && (end is null || d.Date <= end));
}
