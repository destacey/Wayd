namespace Wayd.Common.Domain.Settings;

/// <summary>
/// Organization-wide defaults for scheduling: the values a new team operating model is pre-filled with.
/// </summary>
/// <remarks>
/// Defaults only. A team's own time zone and grace period live on its operating model, and nothing falls back
/// to these when reading a team's.
/// </remarks>
public sealed record SchedulingSettings : ISettingsSection<SchedulingSettings>
{
    public static string Key => "scheduling";

    /// <summary>The IANA time zone id new team operating models start with.</summary>
    public string DefaultTimeZone { get; init; } = "UTC";

    /// <summary>
    /// How many days after a sprint's planned start its commitment is taken, when the team does not start it:
    /// 1 is the end of the first planned day.
    /// </summary>
    public int DefaultCommitmentGraceDays { get; init; } = 1;
}
