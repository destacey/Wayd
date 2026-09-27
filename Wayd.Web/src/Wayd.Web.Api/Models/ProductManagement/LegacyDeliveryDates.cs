using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;

namespace Wayd.Web.Api.Models.ProductManagement;

/// <summary>
/// Keeps the deprecated date inputs of versions and packages working, from before cut and released became
/// instants. A date is read as a day in the organization's default time zone (Settings → Scheduling).
/// </summary>
/// <remarks>
/// Input only, and at the API edge on purpose: commands, queries and the domain are instant-only, and a
/// caller that has moved to <c>CutAt</c>/<c>ReleasedAt</c> never touches this.
/// <para>
/// A date becomes 12:00 on that day in the zone, not midnight: noon sits on the same calendar day for every
/// viewer within eleven hours of the zone, where midnight is the previous evening for everyone west of it.
/// </para>
/// </remarks>
internal static class LegacyDeliveryDates
{
    private static readonly LocalTime Noon = new(12, 0);

    private static async Task<DateTimeZone> Zone(ISettings<SchedulingSettings> schedulingSettings, CancellationToken cancellationToken)
    {
        var settings = await schedulingSettings.Get(cancellationToken);

        return DateTimeZoneProviders.Tzdb.GetZoneOrNull(settings.DefaultTimeZone) ?? DateTimeZone.Utc;
    }

    /// <summary>
    /// The zone to read a request's deprecated dates in, logging that they were used so a caller still
    /// on them can be found before they are removed. UTC, unread, when the request sent none.
    /// </summary>
    public static async Task<DateTimeZone> ZoneForRequest(
        bool usesLegacyDates,
        ISettings<SchedulingSettings> schedulingSettings,
        ILogger logger,
        string action,
        CancellationToken cancellationToken)
    {
        if (!usesLegacyDates)
        {
            return DateTimeZone.Utc;
        }

        logger.LogWarning(
            "{Action} was called with a deprecated date field; the caller should send the instant instead.", action);

        return await Zone(schedulingSettings, cancellationToken);
    }

    public static Instant? ToInstant(LocalDate? day, DateTimeZone zone) =>
        day?.At(Noon).InZoneLeniently(zone).ToInstant();
}
