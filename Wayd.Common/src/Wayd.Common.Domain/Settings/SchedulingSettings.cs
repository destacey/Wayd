using NodaTime;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Domain.Settings;

/// <summary>
/// Organization-wide defaults for scheduling: the values a new team operating model is pre-filled with.
/// </summary>
/// <remarks>
/// Defaults only. A team's own time zone, grace period and working week live on its operating model, and
/// nothing falls back to these when reading a team's.
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

    /// <summary>The days of the week new team operating models work.</summary>
    public IReadOnlyList<IsoDayOfWeek> DefaultWorkingDays { get; init; } = WorkingWeek.MondayToFriday.Days;

    /// <summary>
    /// <see cref="DefaultWorkingDays"/> as a working week. Saved settings are validated, so the Monday-to-Friday
    /// fallback is reached only by a stored row edited outside the app.
    /// </summary>
    public WorkingWeek DefaultWorkingWeek() =>
        WorkingWeek.Create(DefaultWorkingDays) is { IsSuccess: true } week ? week.Value : WorkingWeek.MondayToFriday;

    /// <summary>
    /// Compares the working days by value, so saving the settings unchanged records no change.
    /// </summary>
    public bool Equals(SchedulingSettings? other) =>
        other is not null
        && DefaultTimeZone == other.DefaultTimeZone
        && DefaultCommitmentGraceDays == other.DefaultCommitmentGraceDays
        && DefaultWorkingDays.SequenceEqual(other.DefaultWorkingDays);

    public override int GetHashCode() => HashCode.Combine(DefaultTimeZone, DefaultCommitmentGraceDays, DefaultWorkingDays.Count);
}
