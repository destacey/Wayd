using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintScope;

public class SprintHealthTests
{
    private static readonly Instant Start = Instant.FromUtc(2026, 11, 2, 0, 0);
    private static readonly Instant End = Start.Plus(Duration.FromDays(10));

    private static readonly SprintScopeWindow Window =
        new(Guid.NewGuid(), Start, End, End.Minus(Duration.FromDays(1)), Start, null, false, false, DateTimeZone.Utc);

    private static Instant Day(double days) => Start.Plus(Duration.FromHours(days * 24));

    /// <summary>Half done by day 5, flat over a two-day weekend, then the rest by day 10.</summary>
    private static readonly SprintIdealPoint[] WithWeekend =
    [
        new(Start, 1),
        new(Day(5), 0.5),
        new(Day(7), 0.5),
        new(End, 0),
    ];

    [Theory]
    [InlineData(4.0, AttentionStatus.OnTrack)]
    [InlineData(3.9, AttentionStatus.Watch)]
    [InlineData(2.5, AttentionStatus.Watch)]
    [InlineData(2.4, AttentionStatus.NeedsAttention)]
    public void Measure_BandsHowFarBehindTheIdealLineTheSprintIs(double delivered, AttentionStatus expected)
    {
        // Arrange — the ideal line expects half of 10 done by day 5

        // Act
        var health = SprintHealth.Measure(Window, WithWeekend, committed: 10, delivered, Day(5));

        // Assert
        health.Should().NotBeNull();
        health!.ExpectedShare.Should().BeApproximately(0.5, 1e-9);
        health.DoneShare.Should().BeApproximately(delivered / 10, 1e-9);
        health.Status.Should().Be(expected);
    }

    [Fact]
    public void Measure_ExpectsNoProgressWhereTheIdealLineIsFlat()
    {
        // Arrange — on day 6 the line is still at half; evenly over time it would expect 60%

        // Act
        var health = SprintHealth.Measure(Window, WithWeekend, committed: 10, delivered: 4, Day(6));

        // Assert
        health!.ExpectedShare.Should().BeApproximately(0.5, 1e-9);
        health.Status.Should().Be(AttentionStatus.OnTrack);
    }

    [Fact]
    public void Measure_WithoutALineSpanningNow_ExpectsProgressEvenlyOverTheWindow()
    {
        // Arrange

        // Act
        var health = SprintHealth.Measure(Window, [], committed: 10, delivered: 4, Day(6));

        // Assert
        health!.ExpectedShare.Should().BeApproximately(0.6, 1e-9);
        health.Status.Should().Be(AttentionStatus.Watch);
    }

    [Fact]
    public void Measure_DeliveringMoreThanCommitted_IsOnTrack()
    {
        // Arrange

        // Act
        var health = SprintHealth.Measure(Window, WithWeekend, committed: 10, delivered: 12, Day(8));

        // Assert
        health!.DoneShare.Should().BeApproximately(1.2, 1e-9);
        health.Status.Should().Be(AttentionStatus.OnTrack);
    }

    [Theory]
    [InlineData(-0.5, 10)]
    [InlineData(10, 10)]
    [InlineData(5, 0)]
    public void Measure_WithNothingToMeasureAgainst_ReturnsNull(double day, double committed)
    {
        // Arrange — before the commitment point, at the effective end, or with nothing committed

        // Act
        var health = SprintHealth.Measure(Window, WithWeekend, committed, delivered: 0, Day(day));

        // Assert
        health.Should().BeNull();
    }

    [Theory]
    [InlineData(5.4, AttentionStatus.OnTrack)]
    [InlineData(4.9, AttentionStatus.Watch)]
    [InlineData(3.4, AttentionStatus.NeedsAttention)]
    public void MeasureOverDays_BandsTheWorkDoneAgainstTheShareOfDaysGone(double completed, AttentionStatus expected)
    {
        // Arrange — 6 of 10 days gone, so 60% of 10 is expected done
        var firstDay = new LocalDate(2026, 11, 2);

        // Act
        var health = SprintHealth.MeasureOverDays(firstDay, firstDay.PlusDays(10), firstDay.PlusDays(6), total: 10, completed);

        // Assert
        health!.ExpectedShare.Should().BeApproximately(0.6, 1e-9);
        health.DoneShare.Should().BeApproximately(completed / 10, 1e-9);
        health.Status.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(11, 10, 10)]
    [InlineData(5, 10, 0)]
    [InlineData(5, 0, 10)]
    public void MeasureOverDays_WithNothingToMeasure_ReturnsNull(int todayOffset, int lastDayOffset, double total)
    {
        // Arrange — on the first day, after the last, with no work, or with no days between them
        var firstDay = new LocalDate(2026, 11, 2);

        // Act
        var health = SprintHealth.MeasureOverDays(firstDay, firstDay.PlusDays(lastDayOffset), firstDay.PlusDays(todayOffset), total, completed: 0);

        // Assert
        health.Should().BeNull();
    }
}
