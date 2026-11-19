using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General;

/// <summary>
/// The <see cref="ISystemCalendar"/> over the stored general settings and the clock.
/// </summary>
/// <remarks>
/// Registered by hand rather than through the service marker scan: like <see cref="ISettings{TSection}"/>, it is a
/// primitive any service may read, not a peer. Public because Wolverine's codegen constructs it inline for a handler
/// that takes <see cref="ISystemCalendar"/>.
/// </remarks>
public sealed class SystemCalendar(ISettings<GeneralSettings> generalSettings, IDateTimeProvider dateTimeProvider)
    : ISystemCalendar
{
    private readonly ISettings<GeneralSettings> _generalSettings = generalSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<DateTimeZone> Zone(CancellationToken cancellationToken) =>
        (await _generalSettings.Get(cancellationToken)).Zone();

    public Task<LocalDate> Today(CancellationToken cancellationToken) =>
        DayOf(_dateTimeProvider.Now, cancellationToken);

    public async Task<LocalDate> DayOf(Instant instant, CancellationToken cancellationToken) =>
        instant.InZone(await Zone(cancellationToken)).Date;
}
