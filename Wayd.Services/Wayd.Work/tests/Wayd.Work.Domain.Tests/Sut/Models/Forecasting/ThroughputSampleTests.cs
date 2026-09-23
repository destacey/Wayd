using NodaTime;
using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Domain.Tests.Sut.Models.Forecasting;

public class ThroughputSampleTests
{
    private static readonly LocalDate _from = new(2026, 9, 1);

    [Fact]
    public void FromCompletions_KeepsDaysWithNoCompletions()
    {
        // Arrange
        var completedOn = new[] { _from, _from, _from.PlusDays(3) };

        // Act
        var sample = ThroughputSample.FromCompletions(completedOn, _from, _from.PlusDays(4));

        // Assert
        sample.DailyThroughput.Should().Equal(2, 0, 0, 1, 0);
        sample.Days.Should().Be(5);
        sample.Total.Should().Be(3);
        sample.HasThroughput.Should().BeTrue();
    }

    [Fact]
    public void FromCompletions_NoCompletions_HasNoThroughput()
    {
        // Arrange
        var completedOn = Array.Empty<LocalDate>();

        // Act
        var sample = ThroughputSample.FromCompletions(completedOn, _from, _from.PlusDays(2));

        // Assert
        sample.DailyThroughput.Should().Equal(0, 0, 0);
        sample.HasThroughput.Should().BeFalse();
    }

    [Fact]
    public void FromCompletions_SingleDayWindow_HasOneDay()
    {
        // Arrange
        var completedOn = new[] { _from, _from };

        // Act
        var sample = ThroughputSample.FromCompletions(completedOn, _from, _from);

        // Assert
        sample.DailyThroughput.Should().Equal(2);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void FromCompletions_CompletionOutsideWindow_Throws(int offsetDays)
    {
        // Arrange
        var completedOn = new[] { _from.PlusDays(offsetDays) };

        // Act
        var act = () => ThroughputSample.FromCompletions(completedOn, _from, _from.PlusDays(4));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromCompletions_WindowEndsBeforeItStarts_Throws()
    {
        // Arrange
        var to = _from.PlusDays(-1);

        // Act
        var act = () => ThroughputSample.FromCompletions([], _from, to);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromDailyThroughput_KeepsValuesInOrder()
    {
        // Arrange
        var daily = new[] { 1d, 0d, 3.5d };

        // Act
        var sample = ThroughputSample.FromDailyThroughput(daily);

        // Assert
        sample.DailyThroughput.Should().Equal(1, 0, 3.5);
        sample.Total.Should().Be(4.5);
    }

    [Fact]
    public void FromDailyThroughput_NoDays_Throws()
    {
        // Arrange
        var daily = Array.Empty<double>();

        // Act
        var act = () => ThroughputSample.FromDailyThroughput(daily);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void FromDailyThroughput_InvalidValue_Throws(double value)
    {
        // Arrange
        var daily = new[] { 1d, value };

        // Act
        var act = () => ThroughputSample.FromDailyThroughput(daily);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
