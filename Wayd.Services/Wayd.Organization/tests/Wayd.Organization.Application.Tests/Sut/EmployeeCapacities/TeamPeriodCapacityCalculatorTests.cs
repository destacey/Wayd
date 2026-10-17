using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Models.Planning.Iterations;
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
            new TeamPeriodCapacity(SecondWeek, 5.5m, 5.5m, 0m, true),
            new TeamPeriodCapacity(FirstWeek, 2.5m, 2.5m, 0m, true));
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
        capacity.Should().Equal(new TeamPeriodCapacity(FirstWeek, 5.2m, 4m, 0.4m, true));
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
            new TeamPeriodCapacity(FirstWeek, 2m, 2m, 0m, true),
            new TeamPeriodCapacity(SecondWeek, 3m, 3m, 0m, true));
    }

    [Fact]
    public void Calculate_LeavesOutTheTeamsDaysOffButNotThoseOfTheTeamsBeneathIt()
    {
        // Arrange — the root is off the first Wednesday and Thursday; the child is beneath it all week
        var allocations = Lookup(
            new CapacityAllocation(Ada, Root, true, LongBefore, null, 100),
            new CapacityAllocation(Grace, Child, true, LongBefore, null, 100));
        HierarchyNode[] descendants = [new(Child, [new HierarchyPeriod(FirstWeek.Start, FirstWeek.End)], [])];
        CapacityPeriod withDaysOff = FirstWeek with { DaysOff = [new(new LocalDate(2027, 2, 3)), new(new LocalDate(2027, 2, 4))] };

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [withDaysOff], descendants, Days(Root, Child), allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert — 3 days at the root and the child's 5
        capacity.Should().Equal(new TeamPeriodCapacity(withDaysOff, 8m, 8m, 0m, true));
    }

    [Fact]
    public void Calculate_LeavesOutEachTeamsOwnDaysOffWhereverItIsCounted()
    {
        // Arrange — the root is off the first Monday and the child the first Tuesday, by their own days off
        var allocations = Lookup(
            new CapacityAllocation(Ada, Root, true, LongBefore, null, 100),
            new CapacityAllocation(Grace, Child, true, LongBefore, null, 100));
        HierarchyNode[] descendants = [new(Child, [new HierarchyPeriod(FirstWeek.Start, FirstWeek.End)], [])];
        Dictionary<Guid, IReadOnlyList<TeamDayOff>> teamDaysOff = new()
        {
            [Root] = [new(new LocalDate(2027, 2, 1))],
            [Child] = [new(new LocalDate(2027, 2, 2))],
        };

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [FirstWeek], descendants, Days(Root, Child), allocations, AvailabilityCalculator.AlwaysAvailable, teamDaysOff);

        // Assert — 4 days at the root and 4 for the child
        capacity.Should().Equal(new TeamPeriodCapacity(FirstWeek, 8m, 8m, 0m, true));
    }

    [Fact]
    public void Calculate_TakesAPartDayOffsShareOfEveryonesTimeAndOfTheirTimeAway()
    {
        // Arrange — half of the first Wednesday off at the root and a quarter of Thursday off for the child; Ada
        // is away all of that Wednesday
        var allocations = Lookup(
            new CapacityAllocation(Ada, Root, true, LongBefore, null, 100),
            new CapacityAllocation(Grace, Child, true, LongBefore, null, 100));
        HierarchyNode[] descendants = [new(Child, [new HierarchyPeriod(FirstWeek.Start, FirstWeek.End)], [])];
        var wednesday = new LocalDate(2027, 2, 3);
        CapacityPeriod withHalfDay = FirstWeek with { DaysOff = [new(wednesday, 50)] };
        Dictionary<Guid, IReadOnlyList<TeamDayOff>> teamDaysOff = new() { [Child] = [new(wednesday.PlusDays(1), 25)] };
        var availability = new AvailabilityCalculator(
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, IReadOnlySet<LocalDate>>(),
            null,
            [new TimeAwaySpan(Ada, wednesday, wednesday, 100)]);

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [withHalfDay], descendants, Days(Root, Child), allocations, availability, teamDaysOff);

        // Assert — Ada's 4 other days, and the half of Wednesday her time away takes; Grace's 4.75 days
        capacity.Should().Equal(new TeamPeriodCapacity(withHalfDay, 8.75m, 8.75m, 0.5m, true));
    }

    [Fact]
    public void Calculate_SaysATeamWhosePeopleAreAllAwayStillHasAllocations()
    {
        // Arrange — Ada is away all of the first week
        var allocations = Lookup(new CapacityAllocation(Ada, Root, true, LongBefore, null, 100));
        var availability = new AvailabilityCalculator(
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, IReadOnlySet<LocalDate>>(),
            null,
            [new TimeAwaySpan(Ada, FirstWeek.Start, FirstWeek.End, 100)]);

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(Root, [FirstWeek], [], Days(Root), allocations, availability);

        // Assert
        capacity.Should().Equal(new TeamPeriodCapacity(FirstWeek, 0m, 0m, 5m, true));
    }

    [Fact]
    public void Calculate_SaysATeamOfTeamsHasAllocationsWhenOnlyATeamBeneathItDoes()
    {
        // Arrange — the child is beneath the root for the first week only
        var allocations = Lookup(new CapacityAllocation(Grace, Child, true, LongBefore, null, 100));
        HierarchyNode[] descendants = [new(Child, [new HierarchyPeriod(FirstWeek.Start, FirstWeek.End)], [])];

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [FirstWeek, SecondWeek], descendants, Days(Root, Child), allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity.Select(c => c.HasAllocations).Should().Equal(true, false);
    }

    [Fact]
    public void Calculate_SaysATeamHasNoAllocationsInAPeriodBeforeAnyoneJoins()
    {
        // Arrange — Ada joins the second Monday
        var allocations = Lookup(new CapacityAllocation(Ada, Root, true, SecondWeek.Start, null, 100));

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(
            Root, [FirstWeek, SecondWeek], [], Days(Root), allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity.Select(c => c.HasAllocations).Should().Equal(false, true);
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
        capacity.Should().Equal(new TeamPeriodCapacity(FirstWeek, 4m, 4m, 0m, true));
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
            new TeamPeriodCapacity(FirstWeek, 0m, 0m, 0m, false),
            new TeamPeriodCapacity(SecondWeek, 0m, 0m, 0m, false));
    }

    /// <summary>Monday to Friday of both weeks, with no team calendar, for each team.</summary>
    [Fact]
    public void Calculate_CountsAPartTimerAsTheirWorkingDaysShareOfTheSystems()
    {
        // Arrange — Ada works 4 hours of an 8-hour system day, in full, and is away half of the first Wednesday;
        // Grace has the system's day
        var allocations = Lookup(
            new CapacityAllocation(Ada, Root, true, LongBefore, null, 100),
            new CapacityAllocation(Grace, Root, true, LongBefore, null, 100));
        var availability = new AvailabilityCalculator(
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, IReadOnlySet<LocalDate>>(),
            DefaultCalendar,
            [new TimeAwaySpan(Ada, new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 3), 50)],
            new WorkingDayLengths(8m, new Dictionary<Guid, decimal> { [Ada] = 4m }));

        // Act
        var capacity = TeamPeriodCapacityCalculator.Calculate(Root, [FirstWeek], [], Days(Root), allocations, availability);

        // Assert — Grace 5; Ada five half days less a quarter, which time away took
        capacity.Should().Equal(new TeamPeriodCapacity(FirstWeek, 7.25m, 7.25m, 0.25m, true));
    }

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
