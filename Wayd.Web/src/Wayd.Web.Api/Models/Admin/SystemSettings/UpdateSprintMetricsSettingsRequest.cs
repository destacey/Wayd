using Wayd.Common.Application.SystemSettings.SprintMetrics.Commands;
using Wayd.Common.Domain.Settings;

namespace Wayd.Web.Api.Models.Admin.SystemSettings;

public sealed record UpdateSprintMetricsSettingsRequest
{
    /// <summary>
    /// How many of a team's most recent completed standard sprints its velocity, predictability and say/do
    /// are averaged over. From 3 to 12.
    /// </summary>
    public int SprintWindow { get; set; }

    public UpdateSprintMetricsSettingsCommand ToUpdateSprintMetricsSettingsCommand() => new(SprintWindow);
}

public sealed class UpdateSprintMetricsSettingsRequestValidator : CustomValidator<UpdateSprintMetricsSettingsRequest>
{
    public UpdateSprintMetricsSettingsRequestValidator()
    {
        RuleFor(r => r.SprintWindow)
            .InclusiveBetween(SprintMetricsSettings.MinSprintWindow, SprintMetricsSettings.MaxSprintWindow);
    }
}
