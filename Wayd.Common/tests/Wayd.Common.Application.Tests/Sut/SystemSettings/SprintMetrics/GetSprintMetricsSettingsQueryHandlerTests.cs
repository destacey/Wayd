using FluentAssertions;
using Moq;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.SprintMetrics.Queries;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.SprintMetrics;

public class GetSprintMetricsSettingsQueryHandlerTests
{
    private readonly Mock<ISettings<SprintMetricsSettings>> _settings = new();

    private GetSprintMetricsSettingsQueryHandler CreateHandler() => new(_settings.Object);

    [Fact]
    public async Task Handle_ReturnsTheSprintWindowInEffect()
    {
        // Arrange
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SprintMetricsSettings { SprintWindow = 5 });

        // Act
        var result = await CreateHandler().Handle(new GetSprintMetricsSettingsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.SprintWindow.Should().Be(5);
    }
}
