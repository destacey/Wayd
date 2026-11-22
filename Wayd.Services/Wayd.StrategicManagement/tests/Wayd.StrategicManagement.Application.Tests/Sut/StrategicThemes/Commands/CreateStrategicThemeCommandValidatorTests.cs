using FluentAssertions;
using Wayd.StrategicManagement.Application.StrategicThemes.Commands;

namespace Wayd.StrategicManagement.Application.Tests.Sut.StrategicThemes.Commands;

public sealed class CreateStrategicThemeCommandValidatorTests
{
    private readonly CreateStrategicThemeCommandValidator _sut = new();

    [Fact]
    public async Task Validate_Passes_WhenNameAndDescriptionAreGiven()
    {
        // Arrange
        var command = new CreateStrategicThemeCommand("Grow", "Grow the business.");

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_Fails_WhenDescriptionIsBlank(string description)
    {
        // Arrange
        var command = new CreateStrategicThemeCommand("Grow", description);

        // Act
        var result = await _sut.ValidateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateStrategicThemeCommand.Description));
    }
}
