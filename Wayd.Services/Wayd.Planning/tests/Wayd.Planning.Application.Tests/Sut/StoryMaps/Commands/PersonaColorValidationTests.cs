using Wayd.Planning.Application.StoryMaps.Commands;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Commands;

/// <summary>
/// Covers the persona colour format, which comes from the shared <c>IsHexColor()</c> rule. A length
/// limit alone would let through short strings ("red", "purple7") that render as no colour at all.
/// </summary>
public class PersonaColorValidationTests
{
    private static readonly AddPersonaCommandValidator _addValidator = new();
    private static readonly UpdatePersonaCommandValidator _updateValidator = new();

    private static AddPersonaCommand AddWith(string color) =>
        new(Guid.NewGuid(), "Engineer", null, color);

    private static UpdatePersonaCommand UpdateWith(string color) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Engineer", null, color);

    [Theory]
    [InlineData("#4096FF")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("#AbCdEf")]
    [InlineData("#F00")]
    [InlineData("#abc")]
    public void AddPersona_WithAHexColor_ShouldBeValid(string color)
    {
        // Arrange / Act
        var result = _addValidator.Validate(AddWith(color));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("purple7")]
    [InlineData("red")]
    [InlineData("4096FF")]      // no leading #
    [InlineData("#40")]         // between nothing and the 3-digit form
    [InlineData("#4096")]       // between the 3- and 6-digit forms
    [InlineData("#4096F")]      // one short of the 6-digit form
    [InlineData("#4096FFF")]    // one past it
    [InlineData("#GGGGGG")]     // not hex
    [InlineData("#4096 F")]
    public void AddPersona_WithANonHexColor_ShouldBeInvalid(string color)
    {
        // Arrange / Act
        var result = _addValidator.Validate(AddWith(color));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddPersona_WithAnEmptyColor_ShouldBeInvalid()
    {
        // Arrange / Act
        var result = _addValidator.Validate(AddWith(string.Empty));

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("#4096FF")]
    [InlineData("#ffffff")]
    [InlineData("#F00")]
    public void UpdatePersona_WithAHexColor_ShouldBeValid(string color)
    {
        // Arrange / Act
        var result = _updateValidator.Validate(UpdateWith(color));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("purple7")]
    [InlineData("#4096F")]
    public void UpdatePersona_WithANonHexColor_ShouldBeInvalid(string color)
    {
        // Arrange / Act
        var result = _updateValidator.Validate(UpdateWith(color));

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
