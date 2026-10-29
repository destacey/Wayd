using FluentAssertions;
using Moq;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Application.SystemSettings.ProductTags.Queries;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.Tests.Sut.SystemSettings.ProductTags;

public class GetProductTagSettingsQueryHandlerTests
{
    private readonly Mock<ISettings<ProductTagSettings>> _settings = new();

    private GetProductTagSettingsQueryHandler CreateHandler() => new(_settings.Object);

    [Fact]
    public async Task Handle_ReturnsWhetherFreeTagsAreAllowed()
    {
        // Arrange
        _settings.Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductTagSettings { AllowsFreeTags = true });

        // Act
        var result = await CreateHandler().Handle(new GetProductTagSettingsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.AllowsFreeTags.Should().BeTrue();
    }

    [Fact]
    public void ProductTagSettings_DefaultsToFreeTagsOff()
    {
        // Act
        var defaults = new ProductTagSettings();

        // Assert
        // A missing section reads as its defaults, so a new install keeps product filing curated.
        defaults.AllowsFreeTags.Should().BeFalse();
    }
}
