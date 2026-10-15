using NodaTime;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Organization.Application.EmployeeCapacities;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities;

public class TeamCapacityCalculatorTests
{
    private static readonly Guid Team = Guid.NewGuid();
    private static readonly Guid OtherTeam = Guid.NewGuid();
    private static readonly Guid Employee = Guid.NewGuid();
    private static readonly Guid TeamCalendar = Guid.NewGuid();
    private static readonly Guid RegionalCalendar = Guid.NewGuid();
    private static readonly Guid DefaultCalendar = Guid.NewGuid();

    private static readonly LocalDate Monday = new(2027, 2, 1);
    private static readonly LocalDate Wednesday = new(2027, 2, 3);
    private static readonly LocalDate LongBefore = new(2027, 1, 1);

    // Two working weeks, Monday 2027-02-01 to Friday 2027-02-12, on the team's calendar.
    private static readonly IReadOnlyList<TeamDay> TeamDays =
    [
        .. Enumerable.Range(0, 5).Select(i => new TeamDay(Monday.PlusDays(i), TeamCalendar)),
        .. Enumerable.Range(0, 5).Select(i => new TeamDay(new LocalDate(2027, 2, 8).PlusDays(i), TeamCalendar)),
    ];

    [Fact]
    public void Calculate_CountsAnAllocationAsItsShareOfEachWorkingDay()
    {
        // Arrange
        CapacityAllocation[] allocations = [new(Employee, Team, true, LongBefore, null, 50)];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, TeamDays, allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(5m, 5m, 0m, 0m));
    }

    [Fact]
    public void Calculate_CountsOnlyTheWorkingDaysAnAllocationCovers()
    {
        // Arrange — starts on the first Wednesday and ends on the second Monday: four working days
        CapacityAllocation[] allocations = [new(Employee, Team, true, Wednesday, new LocalDate(2027, 2, 8), 100)];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, TeamDays, allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity[Employee].AllocatedPersonDays.Should().Be(4m);
    }

    [Fact]
    public void Calculate_KeepsARoleThatDoesNotCountOutOfDeliveryCapacity()
    {
        // Arrange
        CapacityAllocation[] allocations =
        [
            new(Employee, Team, true, LongBefore, null, 70),
            new(Employee, Team, false, LongBefore, null, 30),
        ];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, TeamDays, allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(10m, 7m, 0m, 0m));
    }

    [Fact]
    public void Calculate_CountsTimeOnOtherTeamsSeparately()
    {
        // Arrange
        CapacityAllocation[] allocations =
        [
            new(Employee, Team, true, LongBefore, null, 60),
            new(Employee, OtherTeam, true, new LocalDate(2027, 2, 8), null, 40),
        ];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, TeamDays, allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(6m, 6m, 2m, 0m));
    }

    [Fact]
    public void Calculate_WithNoWorkingDays_IsZero()
    {
        // Arrange
        CapacityAllocation[] allocations = [new(Employee, Team, true, LongBefore, null, 100)];

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, [], allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(0m, 0m, 0m, 0m));
    }

    [Fact]
    public void Calculate_TakesTimeAwayOutOfEveryTeamInProportionAndReportsWhatItTook()
    {
        // Arrange — half of the first Wednesday away, on 60% here and 40% elsewhere
        CapacityAllocation[] allocations =
        [
            new(Employee, Team, true, LongBefore, null, 60),
            new(Employee, OtherTeam, true, LongBefore, null, 40),
        ];
        var availability = Availability(timeAway: [new TimeAwaySpan(Employee, Wednesday, Wednesday, 50)]);

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, TeamDays, allocations, availability);

        // Assert — nine and a half days there: 5.7 here with 0.3 taken, 3.8 elsewhere
        capacity[Employee].Should().Be(new PersonCapacity(5.7m, 5.7m, 3.8m, 0.3m));
    }

    [Fact]
    public void Calculate_TakesAHolidayOutWithoutCountingItAsTimeAway()
    {
        // Arrange — the first Wednesday is a holiday in the system default, and the person is also away that day
        CapacityAllocation[] allocations = [new(Employee, Team, true, LongBefore, null, 100)];
        var availability = Availability(
            holidays: new() { [DefaultCalendar] = [Wednesday] },
            timeAway: [new TimeAwaySpan(Employee, Wednesday, Wednesday, 100)]);

        // Act
        var capacity = TeamCapacityCalculator.Calculate(Team, TeamDays, allocations, availability);

        // Assert
        capacity[Employee].Should().Be(new PersonCapacity(9m, 9m, 0m, 0m));
    }

    [Theory]
    [InlineData(2027, 1, 1, null, 50, 5)]
    [InlineData(2027, 2, 3, 8, 100, 4)]
    [InlineData(2027, 2, 13, null, 100, 0)]
    public void PersonDays_IsThePercentOfEachWorkingDayCovered(int startYear, int startMonth, int startDay, int? endDay, int percent, decimal expected)
    {
        // Arrange
        var start = new LocalDate(startYear, startMonth, startDay);
        LocalDate? end = endDay is { } day ? new LocalDate(2027, 2, day) : null;

        // Act
        var personDays = TeamCapacityCalculator.PersonDays(TeamDays, Employee, start, end, percent, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        personDays.Should().Be(expected);
    }

    [Fact]
    public void PersonDays_ForSomeoneWhoFollowsACalendar_TakesItsHolidaysAndNotTheTeamsOrTheDefaults()
    {
        // Arrange — the team and the system default are off the first Wednesday; the calendar the person follows
        // is off the Monday and Tuesday
        var availability = Availability(
            calendars: new() { [Employee] = RegionalCalendar },
            holidays: new() { [TeamCalendar] = [Wednesday], [DefaultCalendar] = [Wednesday], [RegionalCalendar] = [Monday, Monday.PlusDays(1)] });

        // Act
        var personDays = TeamCapacityCalculator.PersonDays(TeamDays, Employee, LongBefore, null, 100, availability);

        // Assert
        personDays.Should().Be(8m);
    }

    [Fact]
    public void AwayPersonDays_IsThePercentOfTheShareOfEachCoveredDayAway()
    {
        // Arrange — a quarter of every day of the first week away, on a 40% allocation that starts on the Wednesday
        var availability = Availability(timeAway: [new TimeAwaySpan(Employee, Monday, Monday.PlusDays(4), 25)]);

        // Act
        var away = TeamCapacityCalculator.AwayPersonDays(TeamDays, Employee, Wednesday, null, 40, availability);

        // Assert — three days at a quarter of 40%
        away.Should().Be(0.3m);
    }

    [Fact]
    public void DeliveryAvailabilityByDay_WhenThreeOfEightShareAHoliday_IsFiveEighths()
    {
        // Arrange — eight people allocated equally; three take a regional calendar with a holiday the team's lacks
        var people = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToList();
        List<CapacityAllocation> allocations = [.. people.Select(p => new CapacityAllocation(p, Team, true, LongBefore, null, 100))];
        var availability = Availability(
            calendars: people.Take(3).ToDictionary(p => p, _ => RegionalCalendar),
            holidays: new() { [RegionalCalendar] = [Wednesday] });

        // Act
        var byDay = TeamCapacityCalculator.DeliveryAvailabilityByDay(TeamDays, allocations, availability);

        // Assert
        byDay[Wednesday].Should().Be(new TeamDayAvailability(8m, 5m));
        byDay[Wednesday].Share.Should().Be(0.625m);
        byDay[Monday].Share.Should().Be(1m);
    }

    [Fact]
    public void DeliveryAvailabilityByDay_WhenOneOfFourIsAwayForHalfADay_IsSevenEighths()
    {
        // Arrange
        var people = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        List<CapacityAllocation> allocations = [.. people.Select(p => new CapacityAllocation(p, Team, true, LongBefore, null, 50))];
        var availability = Availability(timeAway: [new TimeAwaySpan(people[0], Wednesday, Wednesday, 50)]);

        // Act
        var byDay = TeamCapacityCalculator.DeliveryAvailabilityByDay(TeamDays, allocations, availability);

        // Assert
        byDay[Wednesday].Should().Be(new TeamDayAvailability(2m, 1.75m));
        byDay[Wednesday].Share.Should().Be(0.875m);
        byDay[Wednesday.PlusDays(1)].Share.Should().Be(1m);
    }

    [Fact]
    public void DeliveryAvailabilityByDay_WeighsEachPersonByTheirAllocation()
    {
        // Arrange — the person away has a fifth of the team's allocated time
        var away = Guid.NewGuid();
        List<CapacityAllocation> allocations =
        [
            new(away, Team, true, LongBefore, null, 20),
            new(Guid.NewGuid(), Team, true, LongBefore, null, 80),
        ];
        var availability = Availability(timeAway: [new TimeAwaySpan(away, Wednesday, Wednesday, 100)]);

        // Act
        var byDay = TeamCapacityCalculator.DeliveryAvailabilityByDay(TeamDays, allocations, availability);

        // Assert
        byDay[Wednesday].Share.Should().Be(0.8m);
    }

    [Fact]
    public void DeliveryAvailabilityByDay_LeavesOutRolesThatDoNotCountTowardDeliveryCapacity()
    {
        // Arrange — the lead is away, and the lead's time is not delivery capacity
        var lead = Guid.NewGuid();
        List<CapacityAllocation> allocations =
        [
            new(lead, Team, false, LongBefore, null, 100),
            new(Guid.NewGuid(), Team, true, LongBefore, null, 100),
        ];
        var availability = Availability(timeAway: [new TimeAwaySpan(lead, Wednesday, Wednesday, 100)]);

        // Act
        var byDay = TeamCapacityCalculator.DeliveryAvailabilityByDay(TeamDays, allocations, availability);

        // Assert
        byDay[Wednesday].Should().Be(new TeamDayAvailability(1m, 1m));
    }

    [Fact]
    public void DeliveryAvailabilityByDay_HasNoEntryForADayNobodyIsAllocatedOn()
    {
        // Arrange — allocated from the first Wednesday to the first Thursday only
        List<CapacityAllocation> allocations = [new(Employee, Team, true, Wednesday, Wednesday.PlusDays(1), 100)];

        // Act
        var byDay = TeamCapacityCalculator.DeliveryAvailabilityByDay(TeamDays, allocations, AvailabilityCalculator.AlwaysAvailable);

        // Assert
        byDay.Keys.Should().BeEquivalentTo([Wednesday, Wednesday.PlusDays(1)]);
    }

    [Fact]
    public void DailyAllocation_GivesEachCoveredDaysPercentAndWhatItIsWorth()
    {
        // Arrange — 100% the first week and 40% from the second Tuesday, half of the first Wednesday away and
        // the second Wednesday a holiday
        List<CapacityAllocation> allocations =
        [
            new(Employee, Team, true, Monday, Monday.PlusDays(6), 100),
            new(Employee, Team, true, new LocalDate(2027, 2, 9), null, 40),
        ];
        var availability = Availability(
            holidays: new() { [DefaultCalendar] = [new LocalDate(2027, 2, 10)] },
            timeAway: [new TimeAwaySpan(Employee, Wednesday, Wednesday, 50)]);

        // Act
        var days = TeamCapacityCalculator.DailyAllocation(TeamDays, Employee, allocations, availability);

        // Assert — nothing on the second Monday, so no entry for it
        days.Should().Equal(
            new CapacityPlanDay(Monday, 100, 1m),
            new CapacityPlanDay(Monday.PlusDays(1), 100, 1m),
            new CapacityPlanDay(Wednesday, 100, 0.5m),
            new CapacityPlanDay(Monday.PlusDays(3), 100, 1m),
            new CapacityPlanDay(Monday.PlusDays(4), 100, 1m),
            new CapacityPlanDay(new LocalDate(2027, 2, 9), 40, 0.4m),
            new CapacityPlanDay(new LocalDate(2027, 2, 10), 40, 0m),
            new CapacityPlanDay(new LocalDate(2027, 2, 11), 40, 0.4m),
            new CapacityPlanDay(new LocalDate(2027, 2, 12), 40, 0.4m));
    }

    [Fact]
    public void DailyAllocation_AddsUpToWhatPersonDaysGivesForTheSameAllocation()
    {
        // Arrange
        CapacityAllocation allocation = new(Employee, Team, true, Wednesday, null, 60);
        var availability = Availability(timeAway: [new TimeAwaySpan(Employee, Monday, new LocalDate(2027, 2, 9), 25)]);

        // Act
        var days = TeamCapacityCalculator.DailyAllocation(TeamDays, Employee, [allocation], availability);

        // Assert
        days.Sum(d => d.PersonDays).Should().Be(
            TeamCapacityCalculator.PersonDays(TeamDays, Employee, allocation.Start, allocation.End, allocation.Percent, availability));
    }

    [Fact]
    public void DailyConditions_GivesHolidaysTimeAwayAndTimeElsewhereOnlyOnTheDaysTheyFallOn()
    {
        // Arrange — a holiday on the first Wednesday, a quarter of the first Thursday away, 30% elsewhere on the
        // second Monday and 20% more from the second Tuesday
        List<CapacityAllocation> elsewhere =
        [
            new(Employee, OtherTeam, true, new LocalDate(2027, 2, 8), new LocalDate(2027, 2, 8), 30),
            new(Employee, OtherTeam, true, new LocalDate(2027, 2, 8), null, 20),
        ];
        var availability = Availability(
            holidays: new() { [DefaultCalendar] = [Wednesday] },
            timeAway: [new TimeAwaySpan(Employee, Wednesday.PlusDays(1), Wednesday.PlusDays(1), 25)]);

        // Act
        var days = TeamCapacityCalculator.DailyConditions(TeamDays, Employee, elsewhere, availability);

        // Assert
        days.Should().Equal(
            new CapacityPlanPersonDay(Wednesday, true, 0, 0),
            new CapacityPlanPersonDay(Wednesday.PlusDays(1), false, 25, 0),
            new CapacityPlanPersonDay(new LocalDate(2027, 2, 8), false, 0, 50),
            new CapacityPlanPersonDay(new LocalDate(2027, 2, 9), false, 0, 20),
            new CapacityPlanPersonDay(new LocalDate(2027, 2, 10), false, 0, 20),
            new CapacityPlanPersonDay(new LocalDate(2027, 2, 11), false, 0, 20),
            new CapacityPlanPersonDay(new LocalDate(2027, 2, 12), false, 0, 20));
    }

    [Fact]
    public void DailyConditions_ForSomeoneFullyThereWithNothingElsewhere_IsEmpty()
    {
        // Arrange
        var availability = Availability();

        // Act
        var days = TeamCapacityCalculator.DailyConditions(TeamDays, Employee, [], availability);

        // Assert
        days.Should().BeEmpty();
    }

    private static AvailabilityCalculator Availability(
        Dictionary<Guid, Guid>? calendars = null,
        Dictionary<Guid, LocalDate[]>? holidays = null,
        TimeAwaySpan[]? timeAway = null) =>
        new(
            calendars ?? [],
            (holidays ?? []).ToDictionary(c => c.Key, c => (IReadOnlySet<LocalDate>)c.Value.ToHashSet()),
            DefaultCalendar,
            timeAway ?? []);
}
