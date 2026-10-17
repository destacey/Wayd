using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Work.Domain.Models.CadenceMetrics;

namespace Wayd.Work.Domain.Tests.Sut.Models.CadenceMetrics;

public class CadencePeriodTests
{
    // Monday 5 January 2026.
    private static readonly LocalDate Monday = new(2026, 1, 5);

    private static ReportingCadence TwoWeeks(LocalDate anchor) => ReportingCadence.Create(2, anchor).Value;

    private static Instant Noon(LocalDate date) => date.At(new LocalTime(12, 0)).InUtc().ToInstant();

    [Fact]
    public void Recent_ReturnsTheCurrentPeriodThenEndedOnesMostRecentFirst_EachStartingOnTheAnchorsWeekday()
    {
        // Arrange
        var schedule = new[] { new CadenceSchedulePeriod(Monday, null, "UTC", SizingMethod.Count, TwoWeeks(Monday)) };
        var now = Noon(Monday.PlusDays(45));

        // Act
        var periods = CadencePeriod.Recent(schedule, now, window: 3);

        // Assert
        periods.Select(p => p.Start).Should().Equal(Monday.PlusDays(42), Monday.PlusDays(28), Monday.PlusDays(14), Monday);
        periods.Should().AllSatisfy(p => p.Start.DayOfWeek.Should().Be(IsoDayOfWeek.Monday));
        periods.Should().AllSatisfy(p => p.End.Should().Be(p.Start.PlusDays(13)));
        periods[0].IsCurrent.Should().BeTrue();
        periods.Skip(1).Should().AllSatisfy(p => p.IsCurrent.Should().BeFalse());
        periods.Should().AllSatisfy(p => p.IsPartial.Should().BeFalse());
    }

    [Fact]
    public void Recent_StopsOnceTheWindowOfEndedPeriodsIsFull()
    {
        // Arrange
        var schedule = new[] { new CadenceSchedulePeriod(Monday, null, "UTC", SizingMethod.Count, TwoWeeks(Monday)) };
        var now = Noon(Monday.PlusDays(100));

        // Act
        var periods = CadencePeriod.Recent(schedule, now, window: 2);

        // Assert
        periods.Should().HaveCount(3);
        periods.Count(p => !p.IsCurrent).Should().Be(2);
    }

    [Fact]
    public void Recent_WhenTheCadenceChangesMidPeriod_ClosesTheOpenPeriodEarlyAndLeavesEarlierOnesAlone()
    {
        // Arrange — two-week periods from 5 Jan; from Wed 21 Jan a one-week cadence anchored on that day
        var change = Monday.PlusDays(16);
        var schedule = new[]
        {
            new CadenceSchedulePeriod(Monday, change.PlusDays(-1), "UTC", SizingMethod.Count, TwoWeeks(Monday)),
            new CadenceSchedulePeriod(change, null, "UTC", SizingMethod.Count, ReportingCadence.Create(1, change).Value),
        };
        var now = Noon(change.PlusDays(8));

        // Act
        var periods = CadencePeriod.Recent(schedule, now, window: 5);

        // Assert
        periods.Select(p => (p.Start, p.End)).Should().Equal(
            (change.PlusDays(7), change.PlusDays(13)),
            (change, change.PlusDays(6)),
            (Monday.PlusDays(14), change.PlusDays(-1)),
            (Monday, Monday.PlusDays(13)));
        periods[2].IsPartial.Should().BeTrue();
        periods[3].IsPartial.Should().BeFalse();
    }

    [Fact]
    public void Recent_ModelStartingInsideAPeriod_MakesItsFirstPeriodPartial()
    {
        // Arrange — the anchor aligns periods to Mondays, but the model starts on a Thursday
        var start = Monday.PlusDays(3);
        var schedule = new[] { new CadenceSchedulePeriod(start, null, "UTC", SizingMethod.Count, TwoWeeks(Monday)) };
        var now = Noon(Monday.PlusDays(20));

        // Act
        var periods = CadencePeriod.Recent(schedule, now, window: 5);

        // Assert
        periods.Last().Start.Should().Be(start);
        periods.Last().End.Should().Be(Monday.PlusDays(13));
        periods.Last().IsPartial.Should().BeTrue();
    }

    [Fact]
    public void Recent_CountsDaysInTheModelsZone()
    {
        // Arrange
        var schedule = new[] { new CadenceSchedulePeriod(Monday, null, "America/Chicago", SizingMethod.Count, TwoWeeks(Monday)) };
        var now = Noon(Monday.PlusDays(20));

        // Act
        var periods = CadencePeriod.Recent(schedule, now, window: 5);

        // Assert
        var chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
        periods.Last().From.Should().Be(Monday.AtStartOfDayInZone(chicago).ToInstant());
        periods.Last().Until.Should().Be(Monday.PlusDays(14).AtStartOfDayInZone(chicago).ToInstant());
    }

    [Fact]
    public void Recent_WithAModelThatStartsLater_ReturnsNothing()
    {
        // Arrange
        var schedule = new[] { new CadenceSchedulePeriod(Monday, null, "UTC", SizingMethod.Count, TwoWeeks(Monday)) };

        // Act
        var periods = CadencePeriod.Recent(schedule, Noon(Monday.PlusDays(-1)), window: 5);

        // Assert
        periods.Should().BeEmpty();
    }
}
