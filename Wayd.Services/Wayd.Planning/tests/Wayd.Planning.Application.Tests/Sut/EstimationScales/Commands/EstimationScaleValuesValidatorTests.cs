using Wayd.Planning.Application.EstimationScales.Commands;

namespace Wayd.Planning.Application.Tests.Sut.EstimationScales.Commands;

public class EstimationScaleValuesValidatorTests
{
    private readonly EstimationScaleValuesValidator _validator = new();

    [Fact]
    public void Validate_TwoEstimatesAndACard_ShouldPass()
    {
        // Arrange
        List<EstimationScaleValueInput> values = [new("1", true), new("2", true), new("?", false)];

        // Act
        var result = _validator.Validate(values);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0, 3)]
    public void Validate_FewerThanTwoEstimates_ShouldFail(int estimates, int cards)
    {
        // Arrange
        List<EstimationScaleValueInput> values =
        [
            .. Enumerable.Range(1, estimates).Select(i => new EstimationScaleValueInput(i.ToString(), true)),
            .. Enumerable.Range(1, cards).Select(i => new EstimationScaleValueInput($"card{i}", false)),
        ];

        // Act
        var result = _validator.Validate(values);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("An estimation scale must have at least 2 estimate values.");
    }

    [Theory]
    [InlineData("1")]
    [InlineData(" 1 ")]
    public void Validate_DuplicateValue_ShouldFail(string duplicate)
    {
        // Arrange
        List<EstimationScaleValueInput> values = [new("1", true), new("2", true), new(duplicate, false)];

        // Act
        var result = _validator.Validate(values);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Each value on an estimation scale must be unique.");
    }

    [Fact]
    public void Validate_ValueLongerThan32Characters_ShouldFail()
    {
        // Arrange
        List<EstimationScaleValueInput> values = [new("1", true), new("2", true), new(new string('x', 33), false)];

        // Act
        var result = _validator.Validate(values);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
