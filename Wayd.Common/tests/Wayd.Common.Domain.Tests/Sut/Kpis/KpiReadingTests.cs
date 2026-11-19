using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Domain.Tests.Sut.Kpis;

public sealed class KpiReadingTests
{
    private static readonly KpiGoal RaiseTenToTwenty =
        new(KpiTargetDirection.Increase, new KpiBaseline(10, null), KpiTarget.Absolute(20), KpiAtRiskThreshold.Absolute(16));

    [Fact]
    public void Of_ShouldReadEveryDerivedValue_FromTheLatestMeasurement()
    {
        // Arrange
        const double latest = 15;
        const double previous = 12;

        // Act
        var reading = KpiReading.Of(RaiseTenToTwenty, latest, previous);

        // Assert
        reading.Value.Should().Be(15);
        reading.TargetValue.Should().Be(20);
        reading.AtRiskValue.Should().Be(16);
        reading.ChangeFromBaseline.Should().Be(5);
        reading.ChangeFromBaselinePercent.Should().BeApproximately(50, 1e-9);
        reading.ProgressToTarget.Should().BeApproximately(0.5, 1e-9);
        reading.Health.Should().Be(KpiHealth.Unhealthy);
        reading.Trend.Should().Be(KpiTrend.Improving);
    }

    [Fact]
    public void OfValues_ShouldReadTheLatestAlone_WithoutARollingAverage()
    {
        // Act
        var reading = KpiReading.OfValues(RaiseTenToTwenty, [15, 12, 30], null);

        // Assert
        reading.Value.Should().Be(15);
        reading.LatestValue.Should().Be(15);
        reading.AveragedCount.Should().BeNull();
        reading.Trend.Should().Be(KpiTrend.Improving);
    }

    [Fact]
    public void OfValues_ShouldHoldTheRollingAverageToTheGoal_AndTrendItAgainstTheAverageOneEarlier()
    {
        // Arrange — newest first: the average of the latest three is 16, of the three before it 14
        double[] newestFirst = [18, 16, 14, 12, 10];

        // Act
        var reading = KpiReading.OfValues(RaiseTenToTwenty, newestFirst, 3);

        // Assert
        reading.Value.Should().BeApproximately(16, 1e-9);
        reading.LatestValue.Should().Be(18);
        reading.AveragedCount.Should().Be(3);
        reading.Health.Should().Be(KpiHealth.AtRisk);
        reading.ProgressToTarget.Should().BeApproximately(0.6, 1e-9);
        reading.Trend.Should().Be(KpiTrend.Improving);
    }

    [Fact]
    public void OfValues_ShouldAverageWhatThereIs_WhileThereAreFewerValuesThanTheAverageAsksFor()
    {
        // Act
        var reading = KpiReading.OfValues(RaiseTenToTwenty, [18, 14], 7);

        // Assert
        reading.Value.Should().BeApproximately(16, 1e-9);
        reading.AveragedCount.Should().Be(2);
    }

    [Fact]
    public void OfValues_ShouldReadNothing_WithNoValues()
    {
        // Act
        var reading = KpiReading.OfValues(RaiseTenToTwenty, [], 7);

        // Assert
        reading.Value.Should().BeNull();
        reading.AveragedCount.Should().BeNull();
        reading.Trend.Should().Be(KpiTrend.NoData);
    }

    [Theory]
    [InlineData(20d, KpiHealth.Healthy)]
    [InlineData(17d, KpiHealth.AtRisk)]
    [InlineData(16d, KpiHealth.AtRisk)]
    [InlineData(15.9d, KpiHealth.Unhealthy)]
    public void Of_ShouldReadThreeStateHealth_WhenAnAtRiskThresholdIsSet(double latest, KpiHealth expected)
    {
        // Arrange — the goal's threshold is 16 on the way to 20

        // Act
        var reading = KpiReading.Of(RaiseTenToTwenty, latest, null);

        // Assert
        reading.Health.Should().Be(expected);
    }

    [Fact]
    public void Of_ShouldReadOnlyHealthyOrUnhealthy_WithoutAnAtRiskThreshold()
    {
        // Arrange
        var goal = RaiseTenToTwenty with { AtRisk = null };

        // Act
        var reading = KpiReading.Of(goal, 19.5, null);

        // Assert
        reading.Health.Should().Be(KpiHealth.Unhealthy);
        reading.AtRiskValue.Should().BeNull();
    }

    [Fact]
    public void Of_ShouldReadNoChangeOrProgress_WithoutABaseline()
    {
        // Arrange
        var goal = new KpiGoal(KpiTargetDirection.Decrease, KpiBaseline.None, KpiTarget.Absolute(5), null);

        // Act
        var reading = KpiReading.Of(goal, 4, 6);

        // Assert
        reading.ChangeFromBaseline.Should().BeNull();
        reading.ChangeFromBaselinePercent.Should().BeNull();
        reading.ProgressToTarget.Should().BeNull();
        reading.Health.Should().Be(KpiHealth.Healthy);
        reading.Trend.Should().Be(KpiTrend.Improving);
    }

    [Fact]
    public void Of_ShouldReadNothingButTheGoal_WhenNothingHasBeenMeasured()
    {
        // Arrange — no measurement

        // Act
        var reading = KpiReading.Of(RaiseTenToTwenty, null, null);

        // Assert
        reading.Should().Be(new KpiReading(null, 20, 16, null, null, null, null, KpiTrend.NoData));
    }

    [Fact]
    public void Of_ShouldReadTheChangeAsAPercentageOfTheBaselineMagnitude()
    {
        // Arrange
        var goal = new KpiGoal(KpiTargetDirection.Increase, new KpiBaseline(-20, null), KpiTarget.Absolute(0), null);

        // Act
        var reading = KpiReading.Of(goal, -10, null);

        // Assert
        reading.ChangeFromBaseline.Should().Be(10);
        reading.ChangeFromBaselinePercent.Should().BeApproximately(50, 1e-9);
    }

    [Fact]
    public void Of_ShouldReadNoPercentageChange_FromABaselineOfZero()
    {
        // Arrange
        var goal = new KpiGoal(KpiTargetDirection.Increase, new KpiBaseline(0, null), KpiTarget.Absolute(10), null);

        // Act
        var reading = KpiReading.Of(goal, 4, null);

        // Assert
        reading.ChangeFromBaseline.Should().Be(4);
        reading.ChangeFromBaselinePercent.Should().BeNull();
        reading.ProgressToTarget.Should().BeApproximately(0.4, 1e-9);
    }
}
