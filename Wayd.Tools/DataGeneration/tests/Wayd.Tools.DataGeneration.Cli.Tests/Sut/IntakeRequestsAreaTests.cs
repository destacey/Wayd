using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Seeding.Areas;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>How the intake requests area turns a generated size into a value of the desk's scale.</summary>
public class IntakeRequestsAreaTests
{
    private static readonly string[] _tShirtSizes = ["XXS", "XS", "S", "M", "L", "XL", "XXL"];

    [Theory]
    [InlineData(0.0, "XXS")]
    [InlineData(0.5, "M")]
    [InlineData(0.9999, "XXL")]
    public void Estimate_PlacesTheSizeAlongTheScale(double size, string expected)
    {
        // Act
        var estimate = IntakeRequestsArea.Estimate(size, _tShirtSizes);

        // Assert
        estimate.Should().Be(expected);
    }

    [Fact]
    public void Estimate_LeavesARequestUnsizedWhenItHasNoSizeOrItsDeskNoValues()
    {
        // Act & Assert
        IntakeRequestsArea.Estimate(null, _tShirtSizes).Should().BeNull();
        IntakeRequestsArea.Estimate(0.5, null).Should().BeNull();
        IntakeRequestsArea.Estimate(0.5, []).Should().BeNull();
    }
}
