using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Domain.Tests.Sut.Models.Forecasting;

public class CompletionForecastTests
{
    [Theory]
    [InlineData(10, 1)]
    [InlineData(50, 5)]
    [InlineData(85, 9)]
    [InlineData(95, 10)]
    [InlineData(100, 10)]
    public void DaysAtConfidence_ReturnsFewestDaysCoveringThatShareOfTrials(int percent, int expectedDays)
    {
        // Arrange
        var forecast = new CompletionForecast([10, 3, 7, 1, 5, 9, 2, 8, 4, 6], horizonDays: 100);

        // Act
        var days = forecast.DaysAtConfidence(percent);

        // Assert
        days.Should().Be(expectedDays);
    }

    [Fact]
    public void DaysAtConfidence_BeyondHorizon_ReturnsNull()
    {
        // Arrange
        var forecast = new CompletionForecast([1, 2, 3, 11, 11], horizonDays: 10);

        // Act
        var median = forecast.DaysAtConfidence(60);
        var high = forecast.DaysAtConfidence(80);

        // Assert
        median.Should().Be(3);
        high.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void DaysAtConfidence_OutOfRangePercent_Throws(int percent)
    {
        // Arrange
        var forecast = new CompletionForecast([1, 2, 3], horizonDays: 10);

        // Act
        var act = () => forecast.DaysAtConfidence(percent);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0.4)]
    [InlineData(7, 0.6)]
    [InlineData(10, 0.6)]
    [InlineData(20, 0.6)]
    public void ShareFinishedWithin_CountsTrialsFinishedByThatDay(int days, double expected)
    {
        // Arrange
        var forecast = new CompletionForecast([2, 3, 7, 11, 11], horizonDays: 10);

        // Act
        var share = forecast.ShareFinishedWithin(days);

        // Assert
        share.Should().Be(expected);
    }

    [Fact]
    public void FinishedTrialDays_OmitsTrialsBeyondHorizon()
    {
        // Arrange
        var forecast = new CompletionForecast([4, 11, 2, 11, 7], horizonDays: 10);

        // Act
        var finished = forecast.FinishedTrialDays;

        // Assert
        finished.Should().Equal(2, 4, 7);
        forecast.TrialsBeyondHorizon.Should().Be(2);
        forecast.Trials.Should().Be(5);
    }

    [Fact]
    public void LatestOf_TakesTheLatestFinishInEachTrial()
    {
        // Arrange
        var teamA = new CompletionForecast([1, 8, 3], horizonDays: 10);
        var teamB = new CompletionForecast([5, 2, 11], horizonDays: 10);

        // Act
        var combined = CompletionForecast.LatestOf([teamA, teamB]);

        // Assert
        combined.FinishedTrialDays.Should().Equal(5, 8);
        combined.TrialsBeyondHorizon.Should().Be(1);
    }

    [Fact]
    public void LatestOf_DifferentTrialCounts_Throws()
    {
        // Arrange
        var teamA = new CompletionForecast([1, 2], horizonDays: 10);
        var teamB = new CompletionForecast([1, 2, 3], horizonDays: 10);

        // Act
        var act = () => CompletionForecast.LatestOf([teamA, teamB]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LatestOf_DifferentHorizons_Throws()
    {
        // Arrange
        var teamA = new CompletionForecast([1, 2], horizonDays: 10);
        var teamB = new CompletionForecast([1, 2], horizonDays: 20);

        // Act
        var act = () => CompletionForecast.LatestOf([teamA, teamB]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
