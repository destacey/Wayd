using FluentAssertions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.General;
using Wayd.Common.Domain.Settings;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.General;

public class SystemCalendarTests
{
    private readonly Mock<ISettings<GeneralSettings>> _settings = new();

    private SystemCalendar CreateCalendar(string timeZone, Instant now)
    {
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { TimeZone = timeZone });

        return new SystemCalendar(_settings.Object, new TestingDateTimeProvider(new FakeClock(now)));
    }

    [Fact]
    public async Task Today_WhenTheZoneIsStillOnThePreviousDay_ReturnsTheZonesDay()
    {
        // Arrange
        var calendar = CreateCalendar("Pacific/Honolulu", Instant.FromUtc(2026, 6, 2, 5, 0));

        // Act
        var today = await calendar.Today(TestContext.Current.CancellationToken);

        // Assert
        today.Should().Be(new LocalDate(2026, 6, 1));
    }

    [Fact]
    public async Task Today_WhenTheZoneIsAlreadyOnTheNextDay_ReturnsTheZonesDay()
    {
        // Arrange
        var calendar = CreateCalendar("Pacific/Kiritimati", Instant.FromUtc(2026, 6, 1, 12, 0));

        // Act
        var today = await calendar.Today(TestContext.Current.CancellationToken);

        // Assert
        today.Should().Be(new LocalDate(2026, 6, 2));
    }

    [Fact]
    public async Task DayOf_EitherSideOfMidnightInTheZone_ReturnsTheDayItFallsOn()
    {
        // Arrange: midnight in Honolulu is 10:00 UTC.
        var calendar = CreateCalendar("Pacific/Honolulu", Instant.FromUtc(2026, 6, 2, 0, 0));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var before = await calendar.DayOf(Instant.FromUtc(2026, 6, 2, 9, 59, 59), cancellationToken);
        var atMidnight = await calendar.DayOf(Instant.FromUtc(2026, 6, 2, 10, 0), cancellationToken);

        // Assert
        before.Should().Be(new LocalDate(2026, 6, 1));
        atMidnight.Should().Be(new LocalDate(2026, 6, 2));
    }

    [Fact]
    public async Task DayOf_AfterADaylightSavingChange_UsesTheOffsetInEffectOnThatDay()
    {
        // Arrange: Chicago moves to UTC-5 on 2026-03-08, so midnight on the 9th is 05:00 UTC, not 06:00.
        var calendar = CreateCalendar("America/Chicago", Instant.FromUtc(2026, 3, 9, 0, 0));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var before = await calendar.DayOf(Instant.FromUtc(2026, 3, 9, 4, 59), cancellationToken);
        var atMidnight = await calendar.DayOf(Instant.FromUtc(2026, 3, 9, 5, 0), cancellationToken);

        // Assert
        before.Should().Be(new LocalDate(2026, 3, 8));
        atMidnight.Should().Be(new LocalDate(2026, 3, 9));
    }

    [Fact]
    public async Task Today_WhenTheStoredZoneIsUnknown_FallsBackToUtc()
    {
        // Arrange
        var calendar = CreateCalendar("Mars/Olympus_Mons", Instant.FromUtc(2026, 6, 2, 5, 0));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var zone = await calendar.Zone(cancellationToken);
        var today = await calendar.Today(cancellationToken);

        // Assert
        zone.Should().Be(DateTimeZone.Utc);
        today.Should().Be(new LocalDate(2026, 6, 2));
    }

    [Fact]
    public async Task Today_AfterTheZoneIsChanged_ReadsTheNewZone()
    {
        // Arrange
        var now = Instant.FromUtc(2026, 6, 2, 5, 0);
        var calendar = new SystemCalendar(_settings.Object, new TestingDateTimeProvider(new FakeClock(now)));
        _settings.SetupSequence(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { TimeZone = "Pacific/Honolulu" })
            .ReturnsAsync(new GeneralSettings { TimeZone = "UTC" });
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var before = await calendar.Today(cancellationToken);
        var after = await calendar.Today(cancellationToken);

        // Assert
        before.Should().Be(new LocalDate(2026, 6, 1));
        after.Should().Be(new LocalDate(2026, 6, 2));
    }
}
