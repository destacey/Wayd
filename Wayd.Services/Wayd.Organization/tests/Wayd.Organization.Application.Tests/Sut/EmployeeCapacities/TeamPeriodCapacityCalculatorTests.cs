using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class TeamPeriodCapacityCalculatorTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid Child = Guid.NewGuid();
    private static readonly Guid Grandchild = Guid.NewGuid();
    private static readonly Guid Ada = Guid.NewGuid();
    private static readonly Guid Grace = Guid.NewGuid();
    private static readonly Guid DefaultCalendar = Guid.NewGuid();
    private static readonly LocalDate LongBefore = new(2027, 1, 1);

    // Two Monday-to-Friday weeks, each a period.
    private static readonly CapacityPeriod FirstWeek = new(new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 7));
    private static readonly CapacityPeriod SecondWeek = new(new LocalDate(2027, 2, 8), new LocalDate(2027, 2, 14));

    [Fact]
    public void Calculate_GivesATeamsPersonDaysOverEachPeriodsWorkingDaysInOrder()
    {
        // Arrange — 50% from long before, and 100% more for Grace from the second Wednesday
        var allocations = Lookup(
            new CapacityAllocation(Ada, Root, true, LongBefore, null, 50),
            new CapacityAllocation(Grace, Root, true, new LocalDate(2027, 2, 10), null, 100));

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [SecondWeek, FirstWeek], [], Days(Root, Child), allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity.Should().Equal(
            new TeamPeriodCapacity(SecondWeek, 5.5m, 5.5m, 0m),
            new TeamPeriodCapacity(FirstWeek, 2.5m, 2.5m, 0m));
    }

    [Fact]
    public void Calculate_LeavesRolesThatDoNotCountOutOfDeliveryAndReportsTimeAway()
    {
        // Arrange — Ada leads at 40% and is away all of the first Wednesday; the first Thursday is a holiday
        var allocations = Lookup(
            new CapacityAllocation(Ada, Root, false, LongBefore, null, 40),
            new CapacityAllocation(Grace, Root, true, LongBefore, null, 100));
        var availability = new AvailabilityCalculator(
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, IReadOnlySet<LocalDate>> { [DefaultCalendar] = new HashSet<LocalDate> { new(2027, 2, 4) } },
            DefaultCalendar,
            [new TimeAwaySpan(Ada, new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 3), 100)]);

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(Root, [FirstWeek], [], Days(Root), allocations, availability);

        // Assert — Grace 4 days; Ada 3 days at 40%, with 0.4 taken by time away
        capacity.Should().Equal(new TeamPeriodCapacity(FirstWeek, 5.2m, 4m, 0.4m));
    }

    [Fact]
    public void Calculate_ForATeamOfTeams_AddsEachTeamBeneathItOnlyForTheDaysItWasThere()
    {
        // Arrange — the child is beneath the root from the first Thursday to the second Tuesday, and the
        // grandchild beneath the child for the second Monday only
        var allocations = Lookup(
            new CapacityAllocation(Ada, Root, true, LongBefore, null, 20),
            new CapacityAllocation(Ada, Child, true, LongBefore, null, 50),
            new CapacityAllocation(Grace, Grandchild, true, LongBefore, null, 100));
        HierarchyNode[] descendants =
        [
            new(Child, [new HierarchyPeriod(new LocalDate(2027, 2, 4), new LocalDate(2027, 2, 9))],
            [
                new(Grandchild, [new HierarchyPeriod(new LocalDate(2027, 2, 8), new LocalDate(2027, 2, 8))], []),
            ]),
        ];

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [FirstWeek, SecondWeek], descendants, Days(Root, Child, Grandchild), allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert — first week: 1 at the root, 1 for the child's Thursday and Friday; second week: 1 at the root,
        // 1 for the child's Monday and Tuesday, 1 for the grandchild's Monday
        capacity.Should().Equal(
            new TeamPeriodCapacity(FirstWeek, 2m, 2m, 0m),
            new TeamPeriodCapacity(SecondWeek, 3m, 3m, 0m));
    }

    [Fact]
    public void Calculate_CountsEachTeamOverItsOwnWorkingDays()
    {
        // Arrange — the child works Monday to Thursday only
        var allocations = Lookup(new CapacityAllocation(Ada, Child, true, LongBefore, null, 100));
        HierarchyNode[] descendants = [new(Child, [new HierarchyPeriod(FirstWeek.Start, SecondWeek.End)], [])];
        var days = new Dictionary<Guid, IReadOnlyList<TeamDay>>(Days(Root))
        {
            [Child] = [.. Days(Child)[Child].Where(d => d.Date.DayOfWeek != IsoDayOfWeek.Friday)],
        };

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(Root, [FirstWeek], descendants, days, allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity.Should().Equal(new TeamPeriodCapacity(FirstWeek, 4m, 4m, 0m));
    }

    [Fact]
    public void Calculate_ForATeamWithNothingAllocated_IsZeroForEveryPeriod()
    {
        // Arrange
        var allocations = Lookup();

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [FirstWeek, SecondWeek], [], Days(Root), allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity.Should().Equal(
            new TeamPeriodCapacity(FirstWeek, 0m, 0m, 0m),
            new TeamPeriodCapacity(SecondWeek, 0m, 0m, 0m));
    }

    /// <summary>Monday to Friday of both weeks, with no team calendar, for each team.</summary>
    private static Dictionary<Guid, IReadOnlyList<TeamDay>> Days(params Guid[] teams) =>
        teams.ToDictionary(
            t => t,
            _ => (IReadOnlyList<TeamDay>)[.. Enumerable.Range(0, 14)
                .Select(i => FirstWeek.Start.PlusDays(i))
                .Where(d => d.DayOfWeek is not (IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday))
                .Select(d => new TeamDay(d, null))]);

    private static ILookup<Guid, CapacityAllocation> Lookup(params CapacityAllocation[] allocations) =>
        allocations.ToLookup(a => a.TeamId);
}
