using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;

namespace Wayd.Organization.Application.EmployeeCapacities.Dtos;

/// <summary>When an employee is away from every team they are allocated to, and the holidays they take.</summary>
public sealed record EmployeeTimeAwayDto
{
    /// <summary>
    /// Whether the caller may record, change and remove this employee's time away: the employee, anyone they
    /// report up to, and a time away administrator. Each stretch's reason is given only when this is true.
    /// </summary>
    public required bool CanManage { get; set; }

    /// <summary>
    /// How many hours a working day has, from the system settings, so a share of a day can be read and entered in
    /// hours: 25% of an 8-hour day is 2 hours.
    /// </summary>
    public required decimal WorkingDayHours { get; set; }

    /// <summary>
    /// The holiday calendar the employee follows instead of the system default, or null when they follow the
    /// system default.
    /// </summary>
    public NavigationDto? HolidayCalendar { get; set; }

    /// <summary>The stretches of time away, in date order.</summary>
    public required List<EmployeeTimeAwayEntryDto> Entries { get; set; }
}

/// <summary>A stretch of days an employee is away for all or part of each day.</summary>
public sealed record EmployeeTimeAwayEntryDto
{
    /// <summary>The id of the stretch, which the change and remove requests take.</summary>
    public required Guid Id { get; set; }

    /// <summary>The first day away.</summary>
    public required LocalDate Start { get; set; }

    /// <summary>The last day away.</summary>
    public required LocalDate End { get; set; }

    /// <summary>The share of each day the employee is away, from 1 to 100.</summary>
    public required int PercentAway { get; set; }

    /// <summary>
    /// Why the employee is away. Null unless the caller is the employee, someone they report up to, or a time
    /// away administrator.
    /// </summary>
    public TimeAwayReason? Reason { get; set; }
}

/// <summary>A reason for time away, with the code a stretch of time away carries and the requests accept.</summary>
public sealed record TimeAwayReasonDto : CommonEnumDto<TimeAwayReason>;
