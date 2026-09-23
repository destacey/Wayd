using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Domain.Tests.Sut.Models.Forecasting;

public class MonteCarloForecasterTests
{
    private const int Seed = 20260922;

    [Fact]
    public void ForecastCompletion_SingleDaySample_EveryTrialTakesTheSameDays()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([2]);

        // Act
        var forecast = MonteCarloForecaster.ForecastCompletion(sample, remaining: 7, new Random(Seed), trials: 100);

        // Assert
        forecast.FinishedTrialDays.Should().HaveCount(100).And.OnlyContain(d => d == 4);
        forecast.DaysAtConfidence(50).Should().Be(4);
        forecast.DaysAtConfidence(95).Should().Be(4);
    }

    [Fact]
    public void ForecastCompletion_NoRemainingWork_FinishesImmediately()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 1]);

        // Act
        var forecast = MonteCarloForecaster.ForecastCompletion(sample, remaining: 0, new Random(Seed), trials: 100);

        // Assert
        forecast.DaysAtConfidence(95).Should().Be(0);
    }

    [Fact]
    public void ForecastCompletion_AllZeroSample_EveryTrialIsBeyondHorizon()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 0, 0]);

        // Act
        var forecast = MonteCarloForecaster.ForecastCompletion(sample, remaining: 5, new Random(Seed), trials: 100);

        // Assert
        forecast.TrialsBeyondHorizon.Should().Be(100);
        forecast.DaysAtConfidence(50).Should().BeNull();
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(9, 100)]
    public void ForecastCompletion_FinishingOnTheHorizonCountsAsFinished(int horizonDays, int expectedBeyondHorizon)
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([1]);

        // Act
        var forecast = MonteCarloForecaster.ForecastCompletion(sample, remaining: 10, new Random(Seed), trials: 100, horizonDays);

        // Assert
        forecast.TrialsBeyondHorizon.Should().Be(expectedBeyondHorizon);
        forecast.HorizonDays.Should().Be(horizonDays);
    }

    [Fact]
    public void ForecastCompletion_SameSeed_SameForecast()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 1, 2, 0, 3]);

        // Act
        var first = MonteCarloForecaster.ForecastCompletion(sample, remaining: 20, new Random(Seed));
        var second = MonteCarloForecaster.ForecastCompletion(sample, remaining: 20, new Random(Seed));

        // Assert
        second.FinishedTrialDays.Should().Equal(first.FinishedTrialDays);
    }

    [Fact]
    public void ForecastCompletion_HigherConfidence_IsNeverEarlier()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 1, 2, 0, 3, 0, 1]);

        // Act
        var forecast = MonteCarloForecaster.ForecastCompletion(sample, remaining: 30, new Random(Seed));

        // Assert
        var days = new[] { 50, 70, 85, 95 }.Select(p => forecast.DaysAtConfidence(p)!.Value).ToArray();
        days.Should().BeInAscendingOrder();
        days[0].Should().BeLessThan(days[^1]);
    }

    [Fact]
    public void ForecastCompletion_MedianTracksTheSampleRate()
    {
        // Arrange — averages one item a day
        var sample = ThroughputSample.FromDailyThroughput([0, 2]);

        // Act
        var forecast = MonteCarloForecaster.ForecastCompletion(sample, remaining: 100, new Random(Seed));

        // Assert
        forecast.DaysAtConfidence(50).Should().BeInRange(95, 105);
    }

    [Fact]
    public void ForecastCompletion_NegativeRemaining_Throws()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([1]);

        // Act
        var act = () => MonteCarloForecaster.ForecastCompletion(sample, remaining: -1, new Random(Seed));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ForecastBacklogPositions_ReturnsForecastsInTheOrderGiven()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([1]);

        // Act
        var forecasts = MonteCarloForecaster.ForecastBacklogPositions(sample, [3, 1, 2], new Random(Seed), trials: 100);

        // Assert
        forecasts.Select(f => f.DaysAtConfidence(95)).Should().Equal(3, 1, 2);
    }

    [Fact]
    public void ForecastBacklogPositions_ReadsEveryPositionFromTheSameRun()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 1, 2, 0, 3]);

        // Act
        var forecasts = MonteCarloForecaster.ForecastBacklogPositions(sample, [2, 5], new Random(Seed));

        // Assert — separate runs would sometimes finish the later position first
        var (ahead, behind) = (forecasts[0], forecasts[1]);
        Enumerable.Range(0, ahead.Trials).Should().OnlyContain(t => ahead[t] <= behind[t]);
    }

    [Fact]
    public void ForecastBacklogPositions_SinglePosition_MatchesForecastCompletion()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 1, 2, 0, 3]);

        // Act
        var positions = MonteCarloForecaster.ForecastBacklogPositions(sample, [20], new Random(Seed));
        var completion = MonteCarloForecaster.ForecastCompletion(sample, remaining: 20, new Random(Seed));

        // Assert
        positions[0].FinishedTrialDays.Should().Equal(completion.FinishedTrialDays);
    }

    [Fact]
    public void ForecastBacklogPositions_AllZeroSample_OnlyZeroPositionsFinish()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 0]);

        // Act
        var forecasts = MonteCarloForecaster.ForecastBacklogPositions(sample, [0, 1], new Random(Seed), trials: 100);

        // Assert
        forecasts[0].DaysAtConfidence(95).Should().Be(0);
        forecasts[1].TrialsBeyondHorizon.Should().Be(100);
    }

    [Fact]
    public void ForecastBacklogPositions_NegativePosition_Throws()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([1]);

        // Act
        var act = () => MonteCarloForecaster.ForecastBacklogPositions(sample, [1, -1], new Random(Seed));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForecastThroughput_SingleDaySample_EveryTrialFinishesTheSameAmount()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([2]);

        // Act
        var forecast = MonteCarloForecaster.ForecastThroughput(sample, days: 10, new Random(Seed), trials: 100);

        // Assert
        forecast.TrialTotals.Should().HaveCount(100).And.OnlyContain(t => t == 20);
    }

    [Fact]
    public void ForecastThroughput_NoDays_FinishesNothing()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([1, 2]);

        // Act
        var forecast = MonteCarloForecaster.ForecastThroughput(sample, days: 0, new Random(Seed), trials: 100);

        // Assert
        forecast.AmountAtConfidence(50).Should().Be(0);
    }

    [Fact]
    public void ForecastThroughput_HigherConfidence_IsNeverMore()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 1, 2, 0, 3, 0, 1]);

        // Act
        var forecast = MonteCarloForecaster.ForecastThroughput(sample, days: 30, new Random(Seed));

        // Assert
        var amounts = new[] { 50, 70, 85, 95 }.Select(forecast.AmountAtConfidence).ToArray();
        amounts.Should().BeInDescendingOrder();
        amounts[0].Should().BeGreaterThan(amounts[^1]);
    }

    [Fact]
    public void ForecastThroughput_SameSeed_SameForecast()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([0, 1, 2, 0, 3]);

        // Act
        var first = MonteCarloForecaster.ForecastThroughput(sample, days: 14, new Random(Seed));
        var second = MonteCarloForecaster.ForecastThroughput(sample, days: 14, new Random(Seed));

        // Assert
        second.TrialTotals.Should().Equal(first.TrialTotals);
    }

    [Fact]
    public void ForecastThroughput_NegativeDays_Throws()
    {
        // Arrange
        var sample = ThroughputSample.FromDailyThroughput([1]);

        // Act
        var act = () => MonteCarloForecaster.ForecastThroughput(sample, days: -1, new Random(Seed));

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
