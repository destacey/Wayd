using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling;

public sealed class SchedulingSettingsValidator : AbstractValidator<SchedulingSettings>
{
    public const int MaxCommitmentGraceDays = 14;

    /// <summary>The fewest hours a working day may have.</summary>
    public const decimal MinWorkingDayHours = SchedulingSettings.MinWorkingDayHours;

    /// <summary>The most hours a working day may have.</summary>
    public const decimal MaxWorkingDayHours = SchedulingSettings.MaxWorkingDayHours;

    public SchedulingSettingsValidator()
    {
        RuleFor(s => s.DefaultTimeZone)
            .NotEmpty()
            .IsIanaTimeZone();

        RuleFor(s => s.DefaultCommitmentGraceDays)
            .InclusiveBetween(0, MaxCommitmentGraceDays);

        RuleFor(s => s.DefaultWorkingDays)
            .IsWorkingWeek();

        RuleFor(s => s.WorkingDayHours)
            .InclusiveBetween(MinWorkingDayHours, MaxWorkingDayHours);
    }
}
