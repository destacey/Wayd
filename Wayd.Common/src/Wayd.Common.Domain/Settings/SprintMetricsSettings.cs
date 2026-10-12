namespace Wayd.Common.Domain.Settings;

/// <summary>
/// Organization-wide settings for metrics measured across a team's sprints, so every team's are worked out
/// over the same number of sprints.
/// </summary>
public sealed record SprintMetricsSettings : ISettingsSection<SprintMetricsSettings>
{
    /// <summary>The fewest sprints a window may hold: fewer say little about a spread.</summary>
    public const int MinSprintWindow = 3;

    /// <summary>The most sprints a window may hold.</summary>
    public const int MaxSprintWindow = 12;

    public static string Key => "sprintMetrics";

    /// <summary>
    /// How many of a team's most recent completed standard sprints its velocity, predictability and say/do
    /// are averaged over.
    /// </summary>
    public int SprintWindow { get; init; } = 7;
}
