using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.TimeZones.Queries;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.TimeZones;

public class GetTimeZonesQueryHandlerTests
{
    private static GetTimeZonesQueryHandler CreateHandler(Instant now) =>
        new(new TestingDateTimeProvider(new FakeClock(now)));

    [Fact]
    public async Task Handle_ListsUtcAndTheGeographicZones_InIdOrder()
    {
        // Act
        var zones = await CreateHandler(Instant.FromUtc(2026, 1, 15, 12, 0))
            .Handle(new GetTimeZonesQuery(), TestContext.Current.CancellationToken);

        // Assert
        zones.Select(z => z.Id).Should().Contain(["UTC", "America/Chicago", "Europe/London"]);
        zones.Select(z => z.Id).Should().BeInAscendingOrder(StringComparer.Ordinal);
        zones.Select(z => z.Id).Should().OnlyHaveUniqueItems();
        zones.Select(z => z.Id).Should().OnlyContain(id => DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) != null);
    }

    [Fact]
    public async Task Handle_GivesEachZonesOffsetAtTheCurrentInstant()
    {
        // Act
        var winter = await CreateHandler(Instant.FromUtc(2026, 1, 15, 12, 0))
            .Handle(new GetTimeZonesQuery(), TestContext.Current.CancellationToken);
        var summer = await CreateHandler(Instant.FromUtc(2026, 7, 15, 12, 0))
            .Handle(new GetTimeZonesQuery(), TestContext.Current.CancellationToken);

        // Assert
        winter.Single(z => z.Id == "America/Chicago").CurrentOffset.Should().Be("-06:00");
        summer.Single(z => z.Id == "America/Chicago").CurrentOffset.Should().Be("-05:00");
        winter.Single(z => z.Id == "UTC").CurrentOffset.Should().Be("+00:00");
    }
}
