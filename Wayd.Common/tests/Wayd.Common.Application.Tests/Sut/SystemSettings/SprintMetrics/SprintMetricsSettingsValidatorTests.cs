using FluentAssertions;
using Wayd.Common.Application.SystemSettings.SprintMetrics;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.SprintMetrics;

public class SprintMetricsSettingsValidatorTests
{
    private readonly SprintMetricsSettingsValidator _sut = new();

    [Fact]
    public void Validate_AcceptsTheDefaults()
    {
        // Act
        var result = _sut.Validate(new SprintMetricsSettings());

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(SprintMetricsSettings.MinSprintWindow, true)]
    [InlineData(SprintMetricsSettings.MaxSprintWindow, true)]
    [InlineData(SprintMetricsSettings.MinSprintWindow - 1, false)]
    [InlineData(SprintMetricsSettings.MaxSprintWindow + 1, false)]
    public void Validate_BoundsTheSprintWindow(int sprintWindow, bool isValid)
    {
        // Act
        var result = _sut.Validate(new SprintMetricsSettings { SprintWindow = sprintWindow });

        // Assert
        result.IsValid.Should().Be(isValid);
    }
}
