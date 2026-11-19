using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.General;
using Wayd.Common.Domain.Settings;

namespace Wayd.Tests.Shared;

/// <summary>
/// The production <see cref="SystemCalendar"/> over a system time zone the test sets, read against the test's clock.
/// Use it instead of mocking <see cref="ISystemCalendar"/>: a mock that returns a day never runs the zone conversion
/// the test is meant to cover.
/// </summary>
/// <remarks>
/// The default zone is <see cref="DefaultTimeZone"/>, ten hours behind UTC with no daylight saving. A zone alone proves
/// nothing: a test shows it reads the system zone only when its clock stands where the UTC day and the zone's day
/// differ — between 00:00 and 09:59 UTC for the default zone.
/// </remarks>
public sealed class TestingSystemCalendar : ISystemCalendar
{
    /// <summary>The zone every test calendar starts in unless the test names another.</summary>
    public const string DefaultTimeZone = "Pacific/Honolulu";

    private readonly IDateTimeProvider _dateTimeProvider;

    public TestingSystemCalendar(IDateTimeProvider dateTimeProvider, string timeZone = DefaultTimeZone)
    {
        _dateTimeProvider = dateTimeProvider;
        TimeZone = timeZone;
    }

    /// <summary>The stored system time zone id; settable to model an administrator changing it.</summary>
    public string TimeZone { get; set; }

    public Task<DateTimeZone> Zone(CancellationToken cancellationToken) => Calendar().Zone(cancellationToken);

    public Task<LocalDate> Today(CancellationToken cancellationToken) => Calendar().Today(cancellationToken);

    public Task<LocalDate> DayOf(Instant instant, CancellationToken cancellationToken) =>
        Calendar().DayOf(instant, cancellationToken);

    private SystemCalendar Calendar() =>
        new(new FixedSettings<GeneralSettings>(new GeneralSettings { TimeZone = TimeZone }), _dateTimeProvider);

    private sealed class FixedSettings<TSection>(TSection values) : ISettings<TSection>
        where TSection : class, ISettingsSection<TSection>, new()
    {
        private readonly TSection _values = values;

        public Task<TSection> Get(CancellationToken cancellationToken) => Task.FromResult(_values);
    }
}
