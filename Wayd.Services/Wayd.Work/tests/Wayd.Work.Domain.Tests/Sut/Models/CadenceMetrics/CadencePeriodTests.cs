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
    public void Recent_ANewModelWithTheSameCadenceZoneAndSizing_RunsThePeriodOnAcrossIt()
    {
        // Arrange — a model set on Thursday 15 January for another reason, keeping the cadence
        var change = Monday.PlusDays(10);
        var schedule = new[]
        {
            new CadenceSchedulePeriod(Monday, change.PlusDays(-1), "UTC", SizingMethod.Count, TwoWeeks(Monday)),
            new CadenceSchedulePeriod(change, null, "UTC", SizingMethod.Count, TwoWeeks(Monday)),
        };

        // Act
        var periods = CadencePeriod.Recent(schedule, Noon(Monday.PlusDays(20)), window: 5);

        // Assert
        periods.Select(p => (p.Start, p.End)).Should().Equal(
            (Monday.PlusDays(14), Monday.PlusDays(27)),
            (Monday, Monday.PlusDays(13)));
        periods.Should().AllSatisfy(p => p.IsPartial.Should().BeFalse());
    }

    [Fact]
    public void Recent_ANewModelWithAnotherSizingMethod_ClosesTheOpenPeriod()
    {
        // Arrange
        var change = Monday.PlusDays(10);
        var schedule = new[]
        {
            new CadenceSchedulePeriod(Monday, change.PlusDays(-1), "UTC", SizingMethod.Count, TwoWeeks(Monday)),
            new CadenceSchedulePeriod(change, null, "UTC", SizingMethod.StoryPoints, TwoWeeks(Monday)),
        };

        // Act
        var periods = CadencePeriod.Recent(schedule, Noon(Monday.PlusDays(20)), window: 5);

        // Assert
        periods.Select(p => (p.Start, p.End, p.SizingMethod)).Should().Equal(
            (Monday.PlusDays(14), Monday.PlusDays(27), SizingMethod.StoryPoints),
            (change, Monday.PlusDays(13), SizingMethod.StoryPoints),
            (Monday, change.PlusDays(-1), SizingMethod.Count));
    }

    [Fact]
    public void Recent_ConsecutiveModelsInDifferentZones_NeitherOverlapNorLeaveAGap()
    {
        // Arrange — Auckland's day starts hours before Chicago's, so its periods would run past Chicago's start
        var change = Monday.PlusDays(14);
        var schedule = new[]
        {
            new CadenceSchedulePeriod(Monday, change.PlusDays(-1), "Pacific/Auckland", SizingMethod.Count, TwoWeeks(Monday)),
            new CadenceSchedulePeriod(change, null, "America/Chicago", SizingMethod.Count, TwoWeeks(Monday)),
        };

        // Act
        var periods = CadencePeriod.Recent(schedule, Noon(Monday.PlusDays(30)), window: 5);

        // Assert
        periods.Zip(periods.Skip(1)).Should().AllSatisfy(pair => pair.Second.Until.Should().Be(pair.First.From));
    }

    [Fact]
    public void Recent_ModelClosingAfterToday_MakesTheCurrentPeriodPartial()
    {
        // Arrange — a one-week cadence takes over from 22 January, after now
        var change = Monday.PlusDays(17);
        var schedule = new[]
        {
            new CadenceSchedulePeriod(Monday, change.PlusDays(-1), "UTC", SizingMethod.Count, TwoWeeks(Monday)),
            new CadenceSchedulePeriod(change, null, "UTC", SizingMethod.Count, ReportingCadence.Create(1, change).Value),
        };

        // Act
        var periods = CadencePeriod.Recent(schedule, Noon(Monday.PlusDays(15)), window: 5);

        // Assert
        periods[0].Start.Should().Be(Monday.PlusDays(14));
        periods[0].End.Should().Be(change.PlusDays(-1));
        periods[0].IsCurrent.Should().BeTrue();
        periods[0].IsPartial.Should().BeTrue();
    }

    [Fact]
    public void Recent_APeriodAcrossADaylightSavingChange_EndsAtMidnightInTheZone()
    {
        // Arrange — daylight saving ends in Chicago on Sunday 1 November 2026
        var anchor = new LocalDate(2026, 10, 26);
        var schedule = new[] { new CadenceSchedulePeriod(anchor, null, "America/Chicago", SizingMethod.Count, TwoWeeks(anchor)) };
        var chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];

        // Act
        var periods = CadencePeriod.Recent(schedule, Noon(anchor.PlusDays(20)), window: 5);

        // Assert
        var first = periods.Last();
        first.From.Should().Be(Instant.FromUtc(2026, 10, 26, 5, 0));
        first.Until.Should().Be(Instant.FromUtc(2026, 11, 9, 6, 0));
        first.Until.Should().Be(anchor.PlusDays(14).AtStartOfDayInZone(chicago).ToInstant());
    }

    [Fact]
    public void Recent_AtTheInstantAPeriodEnds_ItIsNoLongerCurrent()
    {
        // Arrange
        var schedule = new[] { new CadenceSchedulePeriod(Monday, null, "UTC", SizingMethod.Count, TwoWeeks(Monday)) };
        var endOfFirst = Monday.PlusDays(14).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();

        // Act
        var periods = CadencePeriod.Recent(schedule, endOfFirst, window: 5);

        // Assert
        periods.Single(p => p.Start == Monday).IsCurrent.Should().BeFalse();
        periods[0].Start.Should().Be(Monday.PlusDays(14));
        periods[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void Recent_DaysWithNoCadence_BelongToNoPeriod_AndTheWindowCountsAcrossModels()
    {
        // Arrange — a cadence, two weeks without one, then the cadence again
        var schedule = new[]
        {
            new CadenceSchedulePeriod(Monday, Monday.PlusDays(27), "UTC", SizingMethod.Count, TwoWeeks(Monday)),
            new CadenceSchedulePeriod(Monday.PlusDays(42), null, "UTC", SizingMethod.Count, TwoWeeks(Monday)),
        };

        // Act
        var periods = CadencePeriod.Recent(schedule, Noon(Monday.PlusDays(60)), window: 3);

        // Assert
        periods.Select(p => p.Start).Should().Equal(Monday.PlusDays(56), Monday.PlusDays(42), Monday.PlusDays(14), Monday);
        periods.Count(p => !p.IsCurrent).Should().Be(3);
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
