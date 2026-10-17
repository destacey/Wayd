using NodaTime;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class CapacityReportCalculatorTests
{
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid Child = Guid.NewGuid();
    private static readonly Guid Grandchild = Guid.NewGuid();
    private static readonly Guid Elsewhere = Guid.NewGuid();
    private static readonly Guid Lead = Guid.NewGuid();
    private static readonly Guid Ada = Guid.NewGuid();
    private static readonly Guid Grace = Guid.NewGuid();
    private static readonly Guid DefaultCalendar = Guid.NewGuid();
    private static readonly LocalDate LongBefore = new(2027, 1, 1);

    // Two Monday-to-Friday weeks.
    private static readonly LocalDate From = new(2027, 2, 1);
    private static readonly LocalDate To = new(2027, 2, 14);

    [Fact]
    public void Calculate_ForATeam_MeasuresWhatHolidaysAndTimeAwayTakeFromEachLine()
    {
        // Arrange — Ada at 50% is away all of the first Wednesday; the first Thursday is a holiday; Grace leads at 40%
        CapacityReportAllocation[] allocations =
        [
            new(Ada, Root, null, true, LongBefore, null, 50),
            new(Grace, Root, Lead, false, LongBefore, null, 40),
        ];
        var availability = new AvailabilityCalculator(
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, IReadOnlySet<LocalDate>> { [DefaultCalendar] = new HashSet<LocalDate> { new(2027, 2, 4) } },
            DefaultCalendar,
            [new TimeAwaySpan(Ada, new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 3), 100)]);

        // Act
        var report = CapacityReportCalculator.Calculate(Root, From, To, [], Days(Root), allocations, Weekdays(), availability, null);

        // Assert — ten working days, one a holiday
        report.Nodes.Should().ContainSingle().Which.Should().BeEquivalentTo(new { NodeId = 0, ParentNodeId = (int?)null, TeamId = Root, WorkingDays = 9 });
        report.Lines.Should().HaveCount(2);
        report.Lines.Single(l => l.EmployeeId == Ada).Total.Should().Be(new CapacityMeasures(5m, 0.5m, 0.5m, 4m, 4m));
        report.Lines.Single(l => l.EmployeeId == Grace).Total.Should().Be(new CapacityMeasures(4m, 0.4m, 0m, 3.6m, 0m));
        report.Buckets.Should().BeEmpty();
        report.Lines.Should().OnlyContain(l => l.Buckets.Count == 0);
    }

    [Fact]
    public void Calculate_ForATeamOfTeams_CountsEachTeamBeneathItOnlyForTheDaysItWasThere()
    {
        // Arrange — the child is beneath the root from the first Thursday to the second Tuesday, and the
        // grandchild beneath the child for the second Monday only
        CapacityReportAllocation[] allocations =
        [
            new(Ada, Root, null, true, LongBefore, null, 20),
            new(Ada, Child, null, true, LongBefore, null, 50),
            new(Grace, Grandchild, null, true, LongBefore, null, 100),
        ];
        HierarchyNode[] descendants =
        [
            new(Child, [new HierarchyPeriod(new LocalDate(2027, 2, 4), new LocalDate(2027, 2, 9))],
            [
                new(Grandchild, [new HierarchyPeriod(new LocalDate(2027, 2, 8), new LocalDate(2027, 2, 8))], []),
            ]),
        ];

        // Act
        var report = CapacityReportCalculator.Calculate(
            Root, From, To, descendants, Days(Root, Child, Grandchild), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, null);

        // Assert — the root over ten days, the child over four, the grandchild over one
        report.Nodes.Select(n => (n.NodeId, n.ParentNodeId, n.TeamId, n.WorkingDays)).Should().Equal(
            (0, (int?)null, Root, 10),
            (1, 0, Child, 4),
            (2, 1, Grandchild, 1));
        report.Lines.Select(l => (l.NodeId, l.EmployeeId, l.Total.Allocated)).Should().Equal(
            (0, Ada, 2m),
            (1, Ada, 2m),
            (2, Grace, 1m));
    }

    [Fact]
    public void Calculate_SplitsEachLineIntoBucketsThatAddUpToItsTotal()
    {
        // Arrange — Ada at 100% leaves on the second Wednesday
        CapacityReportAllocation[] allocations = [new(Ada, Root, null, true, LongBefore, new LocalDate(2027, 2, 10), 100)];

        // Act
        var report = CapacityReportCalculator.Calculate(
            Root, From, To, [], Days(Root), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, CapacityReportInterval.Week);

        // Assert
        var line = report.Lines.Should().ContainSingle().Subject;
        line.Total.Allocated.Should().Be(8m);
        line.Buckets.Select(b => b.Allocated).Should().Equal(5m, 3m);
    }

    [Fact]
    public void Calculate_GivesEachPersonTheirTimeOutsideTheReportAndUnallocated()
    {
        // Arrange — Ada has 50% on the root and 30% on a team outside it; from the second Monday the child,
        // where she has 20% more, is beneath the root
        CapacityReportAllocation[] allocations =
        [
            new(Ada, Root, null, true, LongBefore, null, 50),
            new(Ada, Elsewhere, null, true, LongBefore, null, 30),
            new(Ada, Child, null, true, LongBefore, null, 20),
        ];
        HierarchyNode[] descendants = [new(Child, [new HierarchyPeriod(new LocalDate(2027, 2, 8), To)], [])];

        // Act
        var report = CapacityReportCalculator.Calculate(
            Root, From, To, descendants, Days(Root, Child), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, CapacityReportInterval.Week);

        // Assert — outside: 50% the first week, 30% the second; nothing is unallocated
        var person = report.People.Should().ContainSingle().Subject;
        person.Outside.Should().Be(4m);
        person.OutsideBuckets.Should().Equal(2.5m, 1.5m);
        person.Unallocated.Should().Be(0m);
    }

    [Fact]
    public void Calculate_GivesTheTimeAllocatedNowhereAsUnallocated()
    {
        // Arrange
        CapacityReportAllocation[] allocations = [new(Ada, Root, null, true, LongBefore, null, 60)];

        // Act
        var report = CapacityReportCalculator.Calculate(Root, From, To, [], Days(Root), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, null);

        // Assert
        report.People.Should().ContainSingle().Which.Should().BeEquivalentTo(new { EmployeeId = Ada, Outside = 0m, Unallocated = 4m });
    }

    [Fact]
    public void Calculate_KeepsAPersonOnTwoTeamsAsALineOnEach()
    {
        // Arrange
        CapacityReportAllocation[] allocations =
        [
            new(Ada, Child, null, true, LongBefore, null, 50),
            new(Ada, Grandchild, null, true, LongBefore, null, 50),
        ];
        HierarchyNode[] descendants =
        [
            new(Child, [new HierarchyPeriod(From, To)], []),
            new(Grandchild, [new HierarchyPeriod(From, To)], []),
        ];

        // Act
        var report = CapacityReportCalculator.Calculate(
            Root, From, To, descendants, Days(Root, Child, Grandchild), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, null);

        // Assert
        report.Lines.Should().HaveCount(2).And.OnlyContain(l => l.EmployeeId == Ada && l.Total.Allocated == 5m);
        report.People.Should().ContainSingle().Which.Unallocated.Should().Be(0m);
    }

    [Fact]
    public void Calculate_CountsAPersonsTimeOnlyWhileTheirAllocationRuns()
    {
        // Arrange — Ada at 100% on the root leaves on the second Wednesday
        CapacityReportAllocation[] allocations = [new(Ada, Root, null, true, LongBefore, new LocalDate(2027, 2, 10), 100)];

        // Act
        var report = CapacityReportCalculator.Calculate(
            Root, From, To, [], Days(Root), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, CapacityReportInterval.Week);

        // Assert — her last two days are unallocated, and none of her time is outside
        var person = report.People.Should().ContainSingle().Subject;
        person.Unallocated.Should().Be(2m);
        person.UnallocatedBuckets.Should().Equal(0m, 2m);
        person.Outside.Should().Be(0m);
    }

    [Fact]
    public void Calculate_LeavesDaysAPersonWasNotEmployedOutOfTheirUnallocatedTime()
    {
        // Arrange — Ada joined on the first Wednesday and left on the second Wednesday, half allocated throughout
        CapacityReportAllocation[] allocations = [new(Ada, Root, null, true, LongBefore, null, 50)];
        var employment = new Dictionary<Guid, EmploymentPeriod>
        {
            [Ada] = new(new LocalDate(2027, 2, 3), new LocalDate(2027, 2, 10)),
        };

        // Act
        var report = CapacityReportCalculator.Calculate(
            Root, From, To, [], Days(Root), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, null, employment);

        // Assert — half of the six weekdays she was employed
        report.People.Should().ContainSingle().Which.Unallocated.Should().Be(3m);
    }

    [Fact]
    public void Calculate_CountsADayThatIsBothAHolidayAndTimeAwayAsHolidayOnly()
    {
        // Arrange — the first Thursday is a holiday, and Ada is away all of it
        CapacityReportAllocation[] allocations = [new(Ada, Root, null, true, LongBefore, null, 100)];
        var thursday = new LocalDate(2027, 2, 4);
        var availability = new AvailabilityCalculator(
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, IReadOnlySet<LocalDate>> { [DefaultCalendar] = new HashSet<LocalDate> { thursday } },
            DefaultCalendar,
            [new TimeAwaySpan(Ada, thursday, thursday, 100)]);

        // Act
        var report = CapacityReportCalculator.Calculate(Root, From, To, [], Days(Root), allocations, Weekdays(), availability, null);

        // Assert
        report.Lines.Should().ContainSingle().Which.Total.Should().Be(new CapacityMeasures(10m, 1m, 0m, 9m, 9m));
    }

    [Fact]
    public void Calculate_CountsATeamAtTwoPlacesInTheHierarchyOnlyForItsDaysAtEach()
    {
        // Arrange — the child sits under the root the first week and under the grandchild the second
        CapacityReportAllocation[] allocations = [new(Ada, Child, null, true, LongBefore, null, 100)];
        HierarchyNode[] descendants =
        [
            new(Child, [new HierarchyPeriod(From, new LocalDate(2027, 2, 7))], []),
            new(Grandchild, [new HierarchyPeriod(From, To)],
            [
                new(Child, [new HierarchyPeriod(new LocalDate(2027, 2, 8), To)], []),
            ]),
        ];

        // Act
        var report = CapacityReportCalculator.Calculate(
            Root, From, To, descendants, Days(Root, Child, Grandchild), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, null);

        // Assert — a line at each place, adding up to her time once, and none of it outside
        report.Lines.Select(l => (l.NodeId, l.Total.Allocated)).Should().Equal((1, 5m), (3, 5m));
        report.People.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Outside = 0m, Unallocated = 0m });
    }

    [Fact]
    public void Calculate_CountsAPartTimersTimeAsAShareOfAPersonDay()
    {
        // Arrange — Ada works 4 hours against the system's 8, allocated in full
        CapacityReportAllocation[] allocations = [new(Ada, Root, null, true, LongBefore, null, 100)];
        var availability = new AvailabilityCalculator(
            new Dictionary<Guid, Guid>(),
            new Dictionary<Guid, IReadOnlySet<LocalDate>>(),
            null,
            [],
            new WorkingDayLengths(8m, new Dictionary<Guid, decimal> { [Ada] = 4m }));

        // Act
        var report = CapacityReportCalculator.Calculate(Root, From, To, [], Days(Root), allocations, Weekdays(), availability, CapacityReportInterval.Month);

        // Assert
        var line = report.Lines.Should().ContainSingle().Subject;
        line.Total.Allocated.Should().Be(5m);
        line.Buckets.Should().ContainSingle().Which.Allocated.Should().Be(5m);
    }

    [Fact]
    public void Calculate_GivesNoUnallocatedTimeToSomeoneAllocatedOverAWholeDay()
    {
        // Arrange — 80% on the root and 40% on a team outside it
        CapacityReportAllocation[] allocations =
        [
            new(Ada, Root, null, true, LongBefore, null, 80),
            new(Ada, Elsewhere, null, true, LongBefore, null, 40),
        ];

        // Act
        var report = CapacityReportCalculator.Calculate(Root, From, To, [], Days(Root), allocations, Weekdays(), AvailabilityCalculator.AlwaysAvailable, null);

        // Assert
        report.People.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Outside = 4m, Unallocated = 0m });
    }

    [Theory]
    [InlineData(CapacityReportInterval.Week, "2027-01-28..2027-01-31", "2027-02-01..2027-02-07", "2027-02-08..2027-02-10")]
    [InlineData(CapacityReportInterval.Month, "2027-01-28..2027-01-31", "2027-02-01..2027-02-10")]
    public void Buckets_CutsTheFirstAndLastToTheRange(CapacityReportInterval interval, params string[] expected)
    {
        // Arrange — from a Thursday to a Wednesday across a month end

        // Act
        var buckets = CapacityReportCalculator.Buckets(new LocalDate(2027, 1, 28), new LocalDate(2027, 2, 10), interval);

        // Assert
        buckets.Select(b => $"{b.Start:yyyy-MM-dd}..{b.End:yyyy-MM-dd}").Should().Equal(expected);
    }

    private static Dictionary<Guid, IReadOnlyList<TeamDay>> Days(params Guid[] teams) =>
        teams.ToDictionary(t => t, _ => Weekdays());

    private static IReadOnlyList<TeamDay> Weekdays() =>
        [.. Enumerable.Range(0, Period.DaysBetween(From, To) + 1)
            .Select(i => From.PlusDays(i))
            .Where(d => d.DayOfWeek is not (IsoDayOfWeek.Saturday or IsoDayOfWeek.Sunday))
            .Select(d => new TeamDay(d, null))];
}
