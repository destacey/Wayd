using NodaTime;
using Wayd.Common.Domain.Models.Organizations;

namespace Wayd.Common.Domain.Settings;

/// <summary>
/// Organization-wide defaults for scheduling: the values a new team operating model is pre-filled with.
/// </summary>
/// <remarks>
/// The time zone, grace period and working days are defaults only: a team's own live on its operating model,
/// and nothing falls back to them when reading a team's. The holiday calendar is the exception, read for every
/// operating model that names none.
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
    /// The holiday calendar of every team operating model that has none of its own, or null for no holidays.
    /// Unlike the other values this is read, not pre-filled: changing it changes those teams' holidays.
    /// </summary>
    public Guid? DefaultHolidayCalendarId { get; init; }

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
        && DefaultWorkingDays.SequenceEqual(other.DefaultWorkingDays)
        && DefaultHolidayCalendarId == other.DefaultHolidayCalendarId;

    public override int GetHashCode() =>
        HashCode.Combine(DefaultTimeZone, DefaultCommitmentGraceDays, DefaultWorkingDays.Aggregate(0, (hash, day) => HashCode.Combine(hash, day)), DefaultHolidayCalendarId);
}
