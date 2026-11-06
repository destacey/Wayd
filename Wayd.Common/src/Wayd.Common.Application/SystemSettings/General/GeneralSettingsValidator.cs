using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General;

public sealed class GeneralSettingsValidator : AbstractValidator<GeneralSettings>
{
    public GeneralSettingsValidator()
    {
        RuleFor(s => s.TimeZone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .IsIanaTimeZone();
    }
}
