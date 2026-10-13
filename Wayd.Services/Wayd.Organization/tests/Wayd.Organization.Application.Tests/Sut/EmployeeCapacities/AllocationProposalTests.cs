using NodaTime;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class AllocationProposalTests
{
    private static readonly LocalDate From = new(2027, 2, 1);

    [Fact]
    public void RemainingPercent_WithNoAllocations_IsEverything()
    {
        // Arrange
        (LocalDate, LocalDate?, int)[] allocations = [];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From);

        // Assert
        remaining.Should().Be(100);
    }

    [Fact]
    public void RemainingPercent_IsWhatIsLeftOnTheFirstDay()
    {
        // Arrange
        (LocalDate, LocalDate?, int)[] allocations =
        [
            (new LocalDate(2027, 1, 1), null, 30),
            (new LocalDate(2027, 1, 1), new LocalDate(2027, 2, 1), 20),
        ];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From);

        // Assert
        remaining.Should().Be(50);
    }

    [Fact]
    public void RemainingPercent_IsLimitedByALaterBusierDay()
    {
        // Arrange — 30% now, rising to 90% for a week in March
        (LocalDate, LocalDate?, int)[] allocations =
        [
            (new LocalDate(2027, 1, 1), null, 30),
            (new LocalDate(2027, 3, 1), new LocalDate(2027, 3, 7), 60),
        ];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From);

        // Assert
        remaining.Should().Be(10);
    }

    [Fact]
    public void RemainingPercent_UpToALastDay_IgnoresABusierDayAfterIt()
    {
        // Arrange — 30% now, rising to 90% from March
        (LocalDate, LocalDate?, int)[] allocations =
        [
            (new LocalDate(2027, 1, 1), null, 30),
            (new LocalDate(2027, 3, 1), null, 60),
        ];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From, new LocalDate(2027, 2, 28));

        // Assert
        remaining.Should().Be(70);
    }

    [Fact]
    public void RemainingPercent_UpToALastDay_CountsAnAllocationStartingOnIt()
    {
        // Arrange
        (LocalDate, LocalDate?, int)[] allocations =
        [
            (new LocalDate(2027, 1, 1), null, 30),
            (new LocalDate(2027, 2, 28), null, 60),
        ];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From, new LocalDate(2027, 2, 28));

        // Assert
        remaining.Should().Be(10);
    }

    [Fact]
    public void RemainingPercent_IgnoresAllocationsThatEndedBeforeTheFirstDay()
    {
        // Arrange
        (LocalDate, LocalDate?, int)[] allocations = [(new LocalDate(2027, 1, 1), new LocalDate(2027, 1, 31), 100)];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From);

        // Assert
        remaining.Should().Be(100);
    }

    [Fact]
    public void RemainingPercent_WhenFullyAllocated_IsZero()
    {
        // Arrange
        (LocalDate, LocalDate?, int)[] allocations =
        [
            (new LocalDate(2027, 1, 1), null, 60),
            (new LocalDate(2027, 2, 10), null, 40),
        ];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From);

        // Assert
        remaining.Should().Be(0);
    }

    [Fact]
    public void RemainingPercent_IsNeverNegative()
    {
        // Arrange — more than the domain allows, as stored data could still hold
        (LocalDate, LocalDate?, int)[] allocations =
        [
            (new LocalDate(2027, 1, 1), null, 80),
            (new LocalDate(2027, 1, 1), null, 40),
        ];

        // Act
        var remaining = AllocationProposal.RemainingPercent(allocations, From);

        // Assert
        remaining.Should().Be(0);
    }

    [Theory]
    [InlineData(100, 1, new[] { 100 })]
    [InlineData(100, 2, new[] { 50, 50 })]
    [InlineData(50, 3, new[] { 17, 17, 16 })]
    [InlineData(1, 2, new[] { 1, 0 })]
    [InlineData(0, 2, new[] { 0, 0 })]
    public void SplitEvenly_GivesWholeSharesThatAddUp(int percent, int parts, int[] expected)
    {
        // Arrange — the inputs are the case

        // Act
        var shares = AllocationProposal.SplitEvenly(percent, parts);

        // Assert
        shares.Should().Equal(expected);
    }
}
