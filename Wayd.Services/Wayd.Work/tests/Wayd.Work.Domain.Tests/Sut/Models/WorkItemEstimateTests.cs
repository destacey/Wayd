using FluentAssertions;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public sealed class WorkItemEstimateTests
{
    [Theory]
    [InlineData(SizingMethod.StoryPoints, 3d)]
    [InlineData(SizingMethod.Effort, 13d)]
    [InlineData(SizingMethod.Size, 40d)]
    [InlineData(SizingMethod.Count, 1d)]
    public void Of_ReadsTheEstimateTheSizingMethodNames(SizingMethod sizingMethod, double expected)
    {
        // Arrange
        var item = new WorkItemFaker()
            .WithStoryPoints(3)
            .WithEffort(13)
            .WithSize(40)
            .Generate();

        // Act
        var result = WorkItemEstimate.Of(sizingMethod, item);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SizingMethod.StoryPoints)]
    [InlineData(SizingMethod.Effort)]
    [InlineData(SizingMethod.Size)]
    public void Of_WithoutTheNamedEstimate_IsNullEvenWhenAnotherIsSet(SizingMethod sizingMethod)
    {
        // Arrange
        double? storyPoints = sizingMethod == SizingMethod.StoryPoints ? null : 5;
        double? effort = sizingMethod == SizingMethod.Effort ? null : 5;
        double? size = sizingMethod == SizingMethod.Size ? null : 5;

        // Act
        var result = WorkItemEstimate.Of(sizingMethod, storyPoints, effort, size);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Of_WithZero_IsAnEstimate()
    {
        // Act
        var result = WorkItemEstimate.Of(SizingMethod.Effort, null, 0, null);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Of_UnderCount_IsOneWithNoEstimates()
    {
        // Act
        var result = WorkItemEstimate.Of(SizingMethod.Count, null, null, null);

        // Assert
        result.Should().Be(1);
    }
}
