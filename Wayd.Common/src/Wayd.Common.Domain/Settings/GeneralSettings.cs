using NodaTime;

namespace Wayd.Common.Domain.Settings;

/// <summary>
/// Organization-wide settings that belong to no single feature, starting with the system time zone.
/// </summary>
public sealed record GeneralSettings : ISettingsSection<GeneralSettings>
{
    public static string Key => "general";

    /// <summary>
    /// The system time zone, as an IANA id: the zone Wayd reads a calendar day in wherever no team's own zone
    /// applies, and the zone new teams and teams of teams start with.
    /// </summary>
    public string TimeZone { get; init; } = "UTC";

    /// <summary>
    /// <see cref="TimeZone"/> as a zone. Saved settings are validated, so the UTC fallback is reached only by an
    /// id the time zone database has since dropped, which should not fail every read that needs the zone.
    /// </summary>
    public DateTimeZone Zone() => DateTimeZoneProviders.Tzdb.GetZoneOrNull(TimeZone) ?? DateTimeZone.Utc;
}
