using FluentAssertions;
using Moq;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.General.Queries;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.General;

public class GetGeneralSettingsQueryHandlerTests
{
    private readonly Mock<ISettings<GeneralSettings>> _settings = new();

    private GetGeneralSettingsQueryHandler CreateHandler() => new(_settings.Object);

    [Fact]
    public async Task Handle_ReturnsTheTimeZoneInEffect()
    {
        // Arrange
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneralSettings { TimeZone = "America/Chicago" });

        // Act
        var result = await CreateHandler().Handle(new GetGeneralSettingsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.TimeZone.Should().Be("America/Chicago");
    }
}
