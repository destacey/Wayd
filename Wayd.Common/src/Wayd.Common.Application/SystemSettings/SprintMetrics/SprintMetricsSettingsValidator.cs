using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.SprintMetrics;

public sealed class SprintMetricsSettingsValidator : AbstractValidator<SprintMetricsSettings>
{
    public SprintMetricsSettingsValidator()
    {
        RuleFor(s => s.SprintWindow)
            .InclusiveBetween(SprintMetricsSettings.MinSprintWindow, SprintMetricsSettings.MaxSprintWindow);
    }
}
