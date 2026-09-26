using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling;

public sealed class SchedulingSettingsValidator : AbstractValidator<SchedulingSettings>
{
    public const int MaxCommitmentGraceDays = 14;

    public SchedulingSettingsValidator()
    {
        RuleFor(s => s.DefaultTimeZone)
            .NotEmpty()
            .IsIanaTimeZone();

        RuleFor(s => s.DefaultCommitmentGraceDays)
            .InclusiveBetween(0, MaxCommitmentGraceDays);
    }
}
