using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>
/// The capacity of a team, or of a team of teams with every team beneath it, over a date range: who had time on
/// each team, what holidays and time away took from it, and what was left. Capacity is in person-days: one
/// person allocated in full for one system working day.
/// </summary>
/// <remarks>
/// Each <c>lines</c> entry is one person in one role on one team, so every grouping of the report adds the same
/// lines and arrives at the same total. Time away is reported as time lost, never why.
/// </remarks>
public sealed record TeamCapacityReportDto
{
    /// <summary>The first day of the range.</summary>
    public required LocalDate From { get; set; }

    /// <summary>The last day of the range.</summary>
    public required LocalDate To { get; set; }

    /// <summary>How the range is bucketed, or null when it is not.</summary>
    public CapacityReportInterval? Interval { get; set; }

    /// <summary>The buckets of the range in date order, the first and last cut to it. Empty when it is not bucketed.</summary>
    public required IReadOnlyList<TeamCapacityPeriodDto> Buckets { get; set; }

    /// <summary>The hours in a person-day, from the system working day, for showing person-days as hours.</summary>
    public required decimal WorkingDayHours { get; set; }

    /// <summary>The time across every line of the report.</summary>
    public required CapacityMeasuresDto Total { get; set; }

    /// <summary>The time across the report by team member role, by role name with the time in no role last. The lines add up to <c>total</c>.</summary>
    public required IReadOnlyList<TeamCapacityReportRoleDto> Roles { get; set; }

    /// <summary>
    /// The team the report is for, first, then every team beneath it depth first. A team that moved between two
    /// parents in the range appears under each, as a node of its own.
    /// </summary>
    public required IReadOnlyList<TeamCapacityReportTeamDto> Teams { get; set; }

    /// <summary>Everyone with a line in the report, by name, with their time outside it.</summary>
    public required IReadOnlyList<TeamCapacityReportPersonDto> People { get; set; }

    /// <summary>One person's time in one role on one team, by team then person then role.</summary>
    public required IReadOnlyList<TeamCapacityReportLineDto> Lines { get; set; }
}

/// <summary>Time in person-days, from what was allocated to what was left after holidays and time away.</summary>
public sealed record CapacityMeasuresDto
{
    /// <summary>The time allocated over the working days, before holidays and time away.</summary>
    public required decimal AllocatedPersonDays { get; set; }

    /// <summary>The part of <c>allocatedPersonDays</c> on holidays in the calendar each person takes.</summary>
    public required decimal HolidayPersonDays { get; set; }

    /// <summary>The part of <c>allocatedPersonDays</c> taken by time away.</summary>
    public required decimal TimeAwayPersonDays { get; set; }

    /// <summary>What is left of <c>allocatedPersonDays</c> after holidays and time away.</summary>
    public required decimal AvailablePersonDays { get; set; }

    /// <summary>The part of <c>availablePersonDays</c> in roles that count toward delivery capacity.</summary>
    public required decimal DeliveryPersonDays { get; set; }

    /// <summary>The measures as the API returns them.</summary>
    internal static CapacityMeasuresDto From(CapacityMeasures measures) => new()
    {
        AllocatedPersonDays = measures.Allocated,
        HolidayPersonDays = measures.Holidays,
        TimeAwayPersonDays = measures.TimeAway,
        AvailablePersonDays = measures.Available,
        DeliveryPersonDays = measures.Delivery,
    };
}

/// <summary>The time across a capacity report in one team member role.</summary>
public sealed record TeamCapacityReportRoleDto
{
    /// <summary>The role, or null for time allocated in no role.</summary>
    public NavigationDto? Role { get; set; }

    /// <summary>Whether time in the role counts toward delivery capacity.</summary>
    public required bool CountsTowardDeliveryCapacity { get; set; }

    /// <summary>The time in the role.</summary>
    public required CapacityMeasuresDto Total { get; set; }
}

/// <summary>A team as a capacity report counts it: the team the report is for, or one place beneath it.</summary>
public sealed record TeamCapacityReportTeamDto
{
    /// <summary>The node's id within the report: 0 for the team the report is for.</summary>
    public required int NodeId { get; set; }

    /// <summary>The node directly above it, or null for the team the report is for.</summary>
    public int? ParentNodeId { get; set; }

    /// <summary>The team or team of teams.</summary>
    public required TeamNavigationDto Team { get; set; }

    /// <summary>
    /// The days of the range it counts for, in date order: the whole range for the team the report is for, and
    /// for a team beneath it the days it was in the hierarchy.
    /// </summary>
    public required IReadOnlyList<TeamCapacityPeriodDto> Periods { get; set; }

    /// <summary>Its own working days within <c>periods</c> that are not team holidays. A team of teams has the system defaults.</summary>
    public required int WorkingDays { get; set; }
}

/// <summary>
/// A person in a capacity report, with their time outside it over the system default working days, before
/// holidays and time away.
/// </summary>
public sealed record TeamCapacityReportPersonDto
{
    /// <summary>The person.</summary>
    public required NavigationDto Employee { get; set; }

    /// <summary>Their time allocated to teams outside the report, or to a team in it on days it was not in the hierarchy.</summary>
    public required decimal OutsidePersonDays { get; set; }

    /// <summary>Their time allocated to no team.</summary>
    public required decimal UnallocatedPersonDays { get; set; }

    /// <summary><c>outsidePersonDays</c> in each of the report's buckets.</summary>
    public required IReadOnlyList<decimal> OutsideBuckets { get; set; }

    /// <summary><c>unallocatedPersonDays</c> in each of the report's buckets.</summary>
    public required IReadOnlyList<decimal> UnallocatedBuckets { get; set; }
}

/// <summary>One person's time in one role on one team of a capacity report.</summary>
public sealed record TeamCapacityReportLineDto
{
    /// <summary>The <c>nodeId</c> of the team in <c>teams</c>.</summary>
    public required int NodeId { get; set; }

    /// <summary>The person, whose details are in <c>people</c>.</summary>
    public required Guid EmployeeId { get; set; }

    /// <summary>The team member role the time is in, or null for none.</summary>
    public NavigationDto? Role { get; set; }

    /// <summary>Whether time in the role counts toward delivery capacity.</summary>
    public required bool CountsTowardDeliveryCapacity { get; set; }

    /// <summary>The time over the days the team counts for.</summary>
    public required CapacityMeasuresDto Total { get; set; }

    /// <summary>The time in each of the report's buckets.</summary>
    public required IReadOnlyList<CapacityMeasuresDto> Buckets { get; set; }
}
