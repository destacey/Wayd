using Wayd.Common.Application.Dtos;

namespace Wayd.Common.Application.SystemSettings.Scheduling.Dtos;

public sealed record SchedulingSettingsDto
{
    public required string DefaultTimeZone { get; init; }
    public int DefaultCommitmentGraceDays { get; init; }

    /// <summary>The days of the week new team operating models work, Monday first.</summary>
    public required IReadOnlyList<IsoDayOfWeek> DefaultWorkingDays { get; init; }

    /// <summary>
    /// The holiday calendar of every team operating model that has none of its own, or null for none. Carries its
    /// name, so someone who may view the settings but not list the calendars still sees which it is.
    /// </summary>
    public NavigationDto? DefaultHolidayCalendar { get; init; }
}
