using Wayd.Work.Domain.Models.SprintCapacity;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintCapacity;

public class SprintCapacityComparisonTests
{
    // Two sprints that delivered 20 and 40 points with 10 and 30 person-days: 60 points over 40 person-days.
    private static readonly PriorSprintCapacity[] Prior = [new(20, 10m), new(40, 30m)];

    [Fact]
    public void Compare_ExpectsTheRecentVelocityScaledByThisSprintsCapacity()
    {
        // Arrange
        const decimal deliveryPersonDays = 20m;

        // Act
        var comparison = SprintCapacityComparison.Compare(25, deliveryPersonDays, Prior, isCompleted: false);

        // Assert — 1.5 points a person-day over 20 person-days
        comparison.Should().Be(new SprintCapacityComparison(SprintCapacityOutcome.Compared, 30, 2, false));
    }

    [Theory]
    [InlineData(30.5, false, true)]
    [InlineData(30, false, false)]
    [InlineData(29, false, false)]
    [InlineData(30.5, true, false)]
    public void Compare_WarnsOnlyWhenTheScopeIsAboveTheExpectationOnASprintNotYetCompleted(double scope, bool isCompleted, bool expected)
    {
        // Arrange
        const decimal deliveryPersonDays = 20m;

        // Act
        var comparison = SprintCapacityComparison.Compare(scope, deliveryPersonDays, Prior, isCompleted);

        // Assert
        comparison.IsOverCapacity.Should().Be(expected);
        comparison.ExpectedScope.Should().Be(30);
    }

    [Fact]
    public void Compare_WithNoDeliveryCapacity_DoesNotCompare()
    {
        // Arrange
        const decimal deliveryPersonDays = 0m;

        // Act
        var comparison = SprintCapacityComparison.Compare(25, deliveryPersonDays, Prior, isCompleted: false);

        // Assert
        comparison.Should().Be(new SprintCapacityComparison(SprintCapacityOutcome.NoCapacity, null, 0, false));
    }

    [Fact]
    public void Compare_WithNoPriorSprints_DoesNotCompare()
    {
        // Arrange
        PriorSprintCapacity[] prior = [];

        // Act
        var comparison = SprintCapacityComparison.Compare(25, 20m, prior, isCompleted: false);

        // Assert
        comparison.Should().Be(new SprintCapacityComparison(SprintCapacityOutcome.NoPriorSprints, null, 0, false));
    }

    [Fact]
    public void Compare_WhenNoPriorSprintHadCapacity_DoesNotCompare()
    {
        // Arrange
        PriorSprintCapacity[] prior = [new(20, 0m), new(40, 0m)];

        // Act
        var comparison = SprintCapacityComparison.Compare(25, 20m, prior, isCompleted: false);

        // Assert
        comparison.Should().Be(new SprintCapacityComparison(SprintCapacityOutcome.NoPriorCapacity, null, 0, false));
    }

    [Fact]
    public void Compare_LeavesOutPriorSprintsThatHadNoCapacity()
    {
        // Arrange — the sprint with no capacity would double the rate if its velocity were counted
        PriorSprintCapacity[] prior = [.. Prior, new(60, 0m)];

        // Act
        var comparison = SprintCapacityComparison.Compare(25, 20m, prior, isCompleted: false);

        // Assert
        comparison.Should().Be(new SprintCapacityComparison(SprintCapacityOutcome.Compared, 30, 2, false));
    }
}
