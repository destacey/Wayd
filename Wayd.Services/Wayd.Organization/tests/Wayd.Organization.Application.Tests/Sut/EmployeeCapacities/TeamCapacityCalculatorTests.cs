using NodaTime;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class TeamCapacityCalculatorTests
{
    private static readonly Guid Team = Guid.NewGuid();
    private static readonly Guid OtherTeam = Guid.NewGuid();
    private static readonly Guid Employee = Guid.NewGuid();

    // Two working weeks, Monday 2027-02-01 to Friday 2027-02-12.
    private static readonly IReadOnlyList<LocalDate> WorkingDays =
    [
        .. Enumerable.Range(0, 5).Select(i => new LocalDate(2027, 2, 1).PlusDays(i)),
        .. Enumerable.Range(0, 5).Select(i => new LocalDate(2027, 2, 8).PlusDays(i)),
    ];

    [Fact]
    public void Calculate_CountsAnAllocationAsItsShareOfEachWorkingDay()
    {
        // Arrange
        CapacityAllocation[] allocations = [new(Employee, Team, true, new LocalDate(2027, 1, 1), null, 50)];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, WorkingDays, allocations);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(5m, 5m, 0m));
    }

    [Fact]
    public void Calculate_CountsOnlyTheWorkingDaysAnAllocationCovers()
    {
        // Arrange — starts on the first Wednesday and ends on the second Monday: four working days
        CapacityAllocation[] allocations = [new(Employee, Team, true, new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 8), 100)];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, WorkingDays, allocations);

        // Assert
        capacity[Employee].AllocatedPersonDays.Should().Be(4m);
    }

    [Fact]
    public void Calculate_KeepsARoleThatDoesNotCountOutOfDeliveryCapacity()
    {
        // Arrange
        CapacityAllocation[] allocations =
        [
            new(Employee, Team, true, new LocalDate(2027, 1, 1), null, 70),
            new(Employee, Team, false, new LocalDate(2027, 1, 1), null, 30),
        ];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, WorkingDays, allocations);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(10m, 7m, 0m));
    }

    [Fact]
    public void Calculate_CountsTimeOnOtherTeamsSeparately()
    {
        // Arrange
        CapacityAllocation[] allocations =
        [
            new(Employee, Team, true, new LocalDate(2027, 1, 1), null, 60),
            new(Employee, OtherTeam, true, new LocalDate(2027, 2, 8), null, 40),
        ];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, WorkingDays, allocations);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(6m, 6m, 2m));
    }

    [Fact]
    public void Calculate_WithNoWorkingDays_IsZero()
    {
        // Arrange
        CapacityAllocation[] allocations = [new(Employee, Team, true, new LocalDate(2027, 1, 1), null, 100)];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, [], allocations);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(0m, 0m, 0m));
    }
}
