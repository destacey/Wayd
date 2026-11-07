using FluentAssertions;
using NodaTime;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public sealed class DeliverableProgressCalculatorTests
{
    private static readonly LocalDate Start = new(2027, 1, 4);

    private static DeliverableProgressTask Task(decimal progress, int days, Guid? parentId = null) =>
        new(Guid.NewGuid(), parentId, ProjectTaskType.Task, progress, new FlexibleDateRange(Start, Start.PlusDays(days - 1)));

    [Fact]
    public void Calculate_WhenNothingIsLinked_ReturnsNull()
    {
        // Arrange
        var calculator = new DeliverableProgressCalculator([Task(50, 5)]);

        // Act
        var progress = calculator.Calculate([]);

        // Assert
        progress.Should().BeNull("a deliverable with no linked tasks has no progress, not 0%");
    }

    [Fact]
    public void Calculate_WhenNoLinkedTaskIsInThePlan_ReturnsNull()
    {
        // Arrange
        var calculator = new DeliverableProgressCalculator([Task(50, 5)]);

        // Act
        var progress = calculator.Calculate([Guid.NewGuid()]);

        // Assert
        progress.Should().BeNull();
    }

    [Fact]
    public void Calculate_WeightsLinkedTasksByPlannedDuration()
    {
        // Arrange
        var done = Task(100, 10);
        var notStarted = Task(0, 30);
        var calculator = new DeliverableProgressCalculator([done, notStarted]);

        // Act
        var progress = calculator.Calculate([done.Id, notStarted.Id]);

        // Assert
        progress.Should().Be(25m);
    }

    [Fact]
    public void Calculate_ForALinkedSummaryTask_UsesItsLeavesNotItsOwnProgress()
    {
        // Arrange
        var summary = Task(90, 10);
        var done = Task(100, 5, summary.Id);
        var notStarted = Task(0, 5, summary.Id);
        var calculator = new DeliverableProgressCalculator([summary, done, notStarted]);

        // Act
        var progress = calculator.Calculate([summary.Id]);

        // Assert
        progress.Should().Be(50m);
    }

    [Fact]
    public void Calculate_WhenATaskAndItsAncestorAreBothLinked_CountsTheWorkOnce()
    {
        // Arrange
        var summary = Task(0, 10);
        var middle = Task(0, 10, summary.Id);
        var done = Task(100, 5, middle.Id);
        var notStarted = Task(0, 5, summary.Id);
        var calculator = new DeliverableProgressCalculator([summary, middle, done, notStarted]);

        // Act
        var progress = calculator.Calculate([summary.Id, done.Id]);

        // Assert
        progress.Should().Be(50m, "counting the linked leaf twice would give 66.67");
    }

    [Fact]
    public void Calculate_WeighsMilestonesUndatedAndOpenEndedTasksOneDay()
    {
        // Arrange
        var milestone = new DeliverableProgressTask(Guid.NewGuid(), null, ProjectTaskType.Milestone, 100, null);
        var undated = new DeliverableProgressTask(Guid.NewGuid(), null, ProjectTaskType.Task, 100, null);
        var openEnded = new DeliverableProgressTask(Guid.NewGuid(), null, ProjectTaskType.Task, 100, new FlexibleDateRange(Start));
        var notStarted = Task(0, 7);
        var calculator = new DeliverableProgressCalculator([milestone, undated, openEnded, notStarted]);

        // Act
        var progress = calculator.Calculate([milestone.Id, undated.Id, openEnded.Id, notStarted.Id]);

        // Assert
        progress.Should().Be(30m);
    }

    [Fact]
    public void Calculate_RoundsToTwoDecimalPlaces()
    {
        // Arrange
        var done = Task(100, 1);
        var first = Task(0, 1);
        var second = Task(0, 1);
        var calculator = new DeliverableProgressCalculator([done, first, second]);

        // Act
        var progress = calculator.Calculate([done.Id, first.Id, second.Id]);

        // Assert
        progress.Should().Be(33.33m);
    }
}
