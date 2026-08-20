using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Common.Models;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectPhaseTests
{
    [Fact]
    public void UpdatePlannedDates_ShouldSucceed_WhenNoChildrenExist()
    {
        // Arrange
        var phase = new ProjectPhaseFaker().Generate();
        var range = new FlexibleDateRange(new LocalDate(2026, 6, 1), new LocalDate(2026, 6, 10));

        // Act
        var result = phase.UpdatePlannedDates(range, []);

        // Assert
        result.IsSuccess.Should().BeTrue();
        phase.DateRange.Should().Be(range);
    }

    [Fact]
    public void UpdatePlannedDates_ShouldFail_WhenDatesClearedButChildrenHaveDates()
    {
        // Arrange
        var phase = new ProjectPhaseFaker().Generate();
        
        // Create a dated child task under this phase
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 8));
        var childTask = new ProjectTaskFaker()
            .WithProjectPhaseId(phase.Id)
            .WithPlannedDateRange(childRange)
            .Generate();

        // Act
        var result = phase.UpdatePlannedDates(null, [childTask]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be updated to null");
    }

    [Fact]
    public void UpdatePlannedDates_ShouldFail_WhenRangeShrunkAndExcludesChildren()
    {
        // Arrange
        var phase = new ProjectPhaseFaker().Generate();
        
        // Create a child task whose range is June 5 to June 8
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 8));
        var childTask = new ProjectTaskFaker()
            .WithProjectPhaseId(phase.Id)
            .WithPlannedDateRange(childRange)
            .Generate();

        // Propose a range for the phase that starts on June 6 (excluding the child's June 5 start)
        var proposedRange = new FlexibleDateRange(new LocalDate(2026, 6, 6), new LocalDate(2026, 6, 10));

        // Act
        var result = phase.UpdatePlannedDates(proposedRange, [childTask]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("falls outside the selected range");
    }

    [Fact]
    public void UpdatePlannedDates_ShouldSucceed_WhenRangeContainsAllChildren()
    {
        // Arrange
        var phase = new ProjectPhaseFaker().Generate();
        
        // Create a child task whose range is June 5 to June 8
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 8));
        var childTask = new ProjectTaskFaker()
            .WithProjectPhaseId(phase.Id)
            .WithPlannedDateRange(childRange)
            .Generate();

        // Propose a range for the phase that completely contains the child range
        var proposedRange = new FlexibleDateRange(new LocalDate(2026, 6, 4), new LocalDate(2026, 6, 9));

        // Act
        var result = phase.UpdatePlannedDates(proposedRange, [childTask]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        phase.DateRange.Should().Be(proposedRange);
    }

    [Fact]
    public void UpdatePlannedDates_ShouldShiftRootTaskSubtrees_WhenRangeShiftedBySameDuration()
    {
        // Arrange
        var phase = new ProjectPhaseFaker().Generate();
        var originalPhaseRange = new FlexibleDateRange(new LocalDate(2026, 6, 1), new LocalDate(2026, 6, 30));
        phase.UpdatePlannedDates(originalPhaseRange, []).IsSuccess.Should().BeTrue();

        var rootTask = new ProjectTaskFaker()
            .WithProjectPhaseId(phase.Id)
            .WithPlannedDateRange(new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 6, 12)))
            .Generate();
        var childTask = new ProjectTaskFaker()
            .WithProjectPhaseId(phase.Id)
            .WithParentId(rootTask.Id)
            .WithPlannedDateRange(new FlexibleDateRange(new LocalDate(2026, 6, 9), new LocalDate(2026, 6, 10)))
            .Generate();
        rootTask.AddChild(childTask);

        var shiftedPhaseRange = new FlexibleDateRange(new LocalDate(2026, 6, 8), new LocalDate(2026, 7, 7));

        // Act
        var result = phase.UpdatePlannedDates(shiftedPhaseRange, [rootTask]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        phase.DateRange.Should().Be(shiftedPhaseRange);
        rootTask.PlannedDateRange.Should().NotBeNull();
        rootTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 15));
        rootTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 19));
        childTask.PlannedDateRange.Should().NotBeNull();
        childTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 16));
        childTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 17));
    }
}
