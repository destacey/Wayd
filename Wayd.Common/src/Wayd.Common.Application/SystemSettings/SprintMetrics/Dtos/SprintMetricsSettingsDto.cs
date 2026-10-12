namespace Wayd.Common.Application.SystemSettings.SprintMetrics.Dtos;

public sealed record SprintMetricsSettingsDto
{
    /// <summary>
    /// How many of a team's most recent completed standard sprints its velocity, predictability and say/do
    /// are averaged over.
    /// </summary>
    public int SprintWindow { get; init; }
}
