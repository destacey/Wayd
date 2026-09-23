using FluentAssertions;
using Wayd.Work.Application.WorkItems.Forecasting;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Forecasting;

public sealed class ForecastOptionsTests
{
    private readonly ForecastOptionsValidator _validator = new();

    [Fact]
    public void Default_UsesNinetyDaysAndFollowsDependencies()
    {
        // Act
        var options = ForecastOptions.Default;

        // Assert
        options.LookbackDays.Should().Be(90);
        options.IgnoreDependencies.Should().BeFalse();
    }

    [Theory]
    [InlineData(ForecastOptions.MinLookbackDays, true)]
    [InlineData(ForecastOptions.MaxLookbackDays, true)]
    [InlineData(ForecastOptions.MinLookbackDays - 1, false)]
    [InlineData(ForecastOptions.MaxLookbackDays + 1, false)]
    public void Validator_LimitsTheHistoryWindow(int lookbackDays, bool expectedValid)
    {
        // Arrange
        var options = new ForecastOptions { LookbackDays = lookbackDays };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }
}
