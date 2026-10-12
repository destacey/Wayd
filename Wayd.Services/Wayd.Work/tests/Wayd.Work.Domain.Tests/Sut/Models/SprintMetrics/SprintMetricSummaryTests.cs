using Wayd.Work.Domain.Models.SprintMetrics;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintMetrics;

public class SprintMetricSummaryTests
{
    [Fact]
    public void Of_Values_GivesTheirAverageRangeAndPopulationStandardDeviation()
    {
        // Arrange
        double?[] values = [2, 4, 4, 4, 5, 5, 7, 9];

        // Act
        var summary = SprintMetricSummary.Of(values);

        // Assert
        summary.Should().NotBeNull();
        summary!.Average.Should().Be(5);
        summary.Min.Should().Be(2);
        summary.Max.Should().Be(9);
        summary.StandardDeviation.Should().Be(2);
        summary.Sprints.Should().Be(8);
    }

    [Fact]
    public void Of_SomeSprintsWithoutAValue_SummarizesTheRest()
    {
        // Arrange
        double?[] values = [0.5, null, 1];

        // Act
        var summary = SprintMetricSummary.Of(values);

        // Assert
        summary!.Average.Should().Be(0.75);
        summary.Sprints.Should().Be(2);
    }

    [Fact]
    public void Of_NoSprintWithAValue_IsNull()
    {
        // Arrange
        double?[] values = [null, null];

        // Act
        var summary = SprintMetricSummary.Of(values);

        // Assert
        summary.Should().BeNull();
    }
}
