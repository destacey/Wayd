namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// Reads calendar days in the system time zone (<c>GeneralSettings.TimeZone</c>): what "today" is, and which day a
/// moment falls on, wherever no team's own zone applies. A day read in UTC is wrong for most of an evening anywhere
/// west of it, so every calendar day Wayd derives from a moment comes from here or from a team's zone.
/// </summary>
/// <remarks>
/// The zone is a cached setting, so a change saved on one API instance reaches the others within the settings cache's
/// expiry. Code that cannot await — a DTO factory, a domain method — is given the day or the zone by its caller.
/// </remarks>
public interface ISystemCalendar
{
    /// <summary>The system time zone; UTC when the stored zone id is no longer in the time zone database.</summary>
    Task<DateTimeZone> Zone(CancellationToken cancellationToken);

    /// <summary>The current calendar day in the system time zone.</summary>
    Task<LocalDate> Today(CancellationToken cancellationToken);

    /// <summary>The calendar day <paramref name="instant"/> falls on in the system time zone.</summary>
    Task<LocalDate> DayOf(Instant instant, CancellationToken cancellationToken);
}
