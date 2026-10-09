using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Domain.Tests.Sut.Enums.Planning;

public sealed class IterationCategoryExtensionsTests
{
    [Theory]
    [InlineData(IterationCategory.Development, SprintType.Standard)]
    [InlineData(IterationCategory.InnovationAndPlanning, SprintType.NonStandard)]
    public void ToSprintType_ShouldReturnTheDeclaredType(IterationCategory category, SprintType expected)
    {
        // Act
        var result = category.ToSprintType();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToSprintType_ShouldDeclareEveryCategory()
    {
        // Arrange
        var categories = Enum.GetValues<IterationCategory>();

        foreach (var category in categories)
        {
            // Act
            var act = () => category.ToSprintType();

            // Assert
            act.Should().NotThrow(
                $"iteration category '{category}' must declare its sprint type — add it to the ToSprintType switch");
        }
    }
}
