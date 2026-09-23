using Wayd.Work.Domain.Models.Forecasting;

namespace Wayd.Work.Domain.Tests.Sut.Models.Forecasting;

public class ThroughputForecastTests
{
    [Theory]
    [InlineData(10, 10)]
    [InlineData(50, 6)]
    [InlineData(85, 2)]
    [InlineData(95, 1)]
    [InlineData(100, 1)]
    public void AmountAtConfidence_ReturnsMostWorkThatShareOfTrialsFinished(int percent, double expected)
    {
        // Arrange
        var forecast = new ThroughputForecast([10, 3, 7, 1, 5, 9, 2, 8, 4, 6], days: 14);

        // Act
        var amount = forecast.AmountAtConfidence(percent);

        // Assert
        amount.Should().Be(expected);
    }

    [Fact]
    public void TrialTotals_AreSortedAscending()
    {
        // Arrange
        var forecast = new ThroughputForecast([3, 1, 2], days: 5);

        // Act
        var totals = forecast.TrialTotals;

        // Assert
        totals.Should().Equal(1, 2, 3);
        forecast.Days.Should().Be(5);
        forecast.Trials.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void AmountAtConfidence_OutOfRangePercent_Throws(int percent)
    {
        // Arrange
        var forecast = new ThroughputForecast([1, 2, 3], days: 5);

        // Act
        var act = () => forecast.AmountAtConfidence(percent);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
