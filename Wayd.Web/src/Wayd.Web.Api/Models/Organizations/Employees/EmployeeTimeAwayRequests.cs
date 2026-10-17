using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Commands;
using Wayd.Organization.Domain.Models;

namespace Wayd.Web.Api.Models.Organizations.Employees;

/// <summary>
/// A stretch of days an employee is away from every team they are allocated to: leave, business travel, training.
/// </summary>
public sealed record EmployeeTimeAwayRequest
{
    /// <summary>The first day away.</summary>
    public LocalDate Start { get; set; }

    /// <summary>The last day away. The same as the start for a single day.</summary>
    public LocalDate End { get; set; }

    /// <summary>The share of each day the employee is away, from 1 to 100: 100 for whole days, 50 for half days.</summary>
    public int PercentAway { get; set; }

    /// <summary>
    /// Why the employee is away. Shown only to the employee, the people they report up to and time away
    /// administrators.
    /// </summary>
    public TimeAwayReason Reason { get; set; }

    public RecordEmployeeTimeAwayCommand ToRecordEmployeeTimeAwayCommand(Guid employeeId) =>
        new(employeeId, Start, End, PercentAway, Reason);

    public ChangeEmployeeTimeAwayCommand ToChangeEmployeeTimeAwayCommand(Guid employeeId, Guid timeAwayId) =>
        new(employeeId, timeAwayId, Start, End, PercentAway, Reason);
}

public sealed class EmployeeTimeAwayRequestValidator : CustomValidator<EmployeeTimeAwayRequest>
{
    public EmployeeTimeAwayRequestValidator()
    {
        RuleFor(r => r.End)
            .GreaterThanOrEqualTo(r => r.Start)
            .WithMessage("Time away cannot end before it starts.");

        RuleFor(r => r.PercentAway)
            .InclusiveBetween(1, EmployeeTimeAway.FullDay);

        RuleFor(r => r.Reason)
            .IsInEnum();
    }
}

/// <summary>Sets the holiday calendar an employee follows instead of the system default.</summary>
public sealed record ChangeEmployeeHolidayCalendarRequest
{
    /// <summary>
    /// The shared holiday calendar the employee follows, such as their country's, or null to have them follow the
    /// system default.
    /// </summary>
    public Guid? HolidayCalendarId { get; set; }
}

/// <summary>Sets how many hours an employee's working day has instead of the system's.</summary>
public sealed record ChangeEmployeeWorkingDayHoursRequest
{
    /// <summary>
    /// The hours of the employee's working day, from 1 to 24 with at most two decimal places, such as 4 for
    /// someone contracted for half days; or null to give them the system's.
    /// </summary>
    public decimal? WorkingDayHours { get; set; }
}

public sealed class ChangeEmployeeWorkingDayHoursRequestValidator : CustomValidator<ChangeEmployeeWorkingDayHoursRequest>
{
    public ChangeEmployeeWorkingDayHoursRequestValidator()
    {
        RuleFor(r => r.WorkingDayHours)
            .InclusiveBetween(SchedulingSettings.MinWorkingDayHours, SchedulingSettings.MaxWorkingDayHours)
            .PrecisionScale(2 + SchedulingSettings.WorkingDayHoursDecimals, SchedulingSettings.WorkingDayHoursDecimals, ignoreTrailingZeros: true);
    }
}
