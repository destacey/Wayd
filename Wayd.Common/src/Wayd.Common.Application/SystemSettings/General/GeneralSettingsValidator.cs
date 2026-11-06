using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General;

/// <summary>The rules every save of the general settings must pass, whoever makes it.</summary>
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
