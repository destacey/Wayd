using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Organization.Domain.Models;

namespace Wayd.Organization.Domain.Tests.Sut.Models;

public class EmployeeCapacityTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 12, 12, 0);
    private static readonly LocalDate Jan1 = new(2027, 1, 1);
    private static readonly LocalDate Feb1 = new(2027, 2, 1);
    private static readonly LocalDate Feb7 = new(2027, 2, 7);

    private readonly Guid _teamA = Guid.NewGuid();
    private readonly Guid _teamB = Guid.NewGuid();
    private readonly Guid _engineer = Guid.NewGuid();
    private readonly Guid _lead = Guid.NewGuid();

    [Fact]
    public void Create_UsesTheEmployeeIdAndAllocatesNothing()
    {
        // Arrange
        var employeeId = Guid.NewGuid();

        // Act
        var capacity = EmployeeCapacity.Create(employeeId);

        // Assert
        capacity.Id.Should().Be(employeeId);
        capacity.Allocations.Should().BeEmpty();
        capacity.Revision.Should().Be(0);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeAllocations_AddsTheAllocationAndRaisesEvent()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamA, _engineer, Jan1, null, 60)], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var allocation = capacity.Allocations.Should().ContainSingle().Subject;
        allocation.EmployeeId.Should().Be(capacity.Id);
        allocation.ToDetails().Should().Be(new EmployeeAllocationDetails(_teamA, _engineer, Jan1, null, 60));
        capacity.Revision.Should().Be(1);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeAllocationsChangedEvent>().Subject;
        raised.Id.Should().Be(capacity.Id);
        raised.Added.Should().Equal(new EmployeeAllocationDetails(_teamA, _engineer, Jan1, null, 60));
        raised.Removed.Should().BeEmpty();
        raised.Allocations.Should().Equal(raised.Added);
    }

    [Fact]
    public void ChangeAllocations_WhenAnotherTeamWouldTakeThePersonOverFull_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 60));

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamB, null, Feb1, Feb7, 41)], EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("101%").And.Contain("2027-02-01");
        capacity.Allocations.Should().ContainSingle();
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeAllocations_WhenAnotherTeamTakesExactlyWhatIsLeft_Succeeds()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 60));

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamB, null, Feb1, Feb7, 40)], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Should().HaveCount(2);
    }

    [Fact]
    public void ChangeAllocations_WhenTheRangesDoNotOverlap_AllowsBothTeamsInFull()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, Feb1.PlusDays(-1), 100));

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamB, null, Feb1, null, 100)], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Should().HaveCount(2);
    }

    [Fact]
    public void ChangeAllocations_WithTwoRolesOnOneTeam_HoldsBothAndCountsBothTowardFull()
    {
        // Arrange
        var capacity = WithAllocations(
            new AllocationChange(_teamA, _lead, Jan1, null, 30),
            new AllocationChange(_teamA, _engineer, Jan1, null, 70));

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamB, null, Jan1, null, 1)], EventActor.System, Now);

        // Assert
        capacity.Allocations.Should().HaveCount(2);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ChangeAllocations_InsideAnOpenEndedAllocation_SplitsItAndKeepsItBeforeAndAfter()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 50));
        var original = capacity.Allocations.Single();

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamA, null, Feb1, Feb7, 100)], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Select(a => a.ToDetails()).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, null, Jan1, Feb1.PlusDays(-1), 50),
            new EmployeeAllocationDetails(_teamA, null, Feb1, Feb7, 100),
            new EmployeeAllocationDetails(_teamA, null, Feb7.PlusDays(1), null, 50),
        ]);
        capacity.Allocations.Single(a => a.Start == Jan1).Should().BeSameAs(original);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeAllocationsChangedEvent>().Subject;
        raised.Removed.Should().Equal(new EmployeeAllocationDetails(_teamA, null, Jan1, null, 50));
        raised.Added.Should().HaveCount(3);
        raised.Allocations.Should().HaveCount(3);
    }

    [Fact]
    public void ChangeAllocations_BackToTheSurroundingPercent_JoinsTheAllocationUpAgain()
    {
        // Arrange
        var capacity = WithAllocations(
            new AllocationChange(_teamA, null, Jan1, null, 50),
            new AllocationChange(_teamA, null, Feb1, Feb7, 100));

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamA, null, Feb1, Feb7, 50)], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Should().ContainSingle()
            .Which.ToDetails().Should().Be(new EmployeeAllocationDetails(_teamA, null, Jan1, null, 50));
    }

    [Fact]
    public void ChangeAllocations_ToZero_EndsTheAllocationForTheRangeOnly()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 50));

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamA, null, Feb1, Feb7, 0)], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Select(a => a.ToDetails()).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, null, Jan1, Feb1.PlusDays(-1), 50),
            new EmployeeAllocationDetails(_teamA, null, Feb7.PlusDays(1), null, 50),
        ]);
    }

    [Fact]
    public void ChangeAllocations_ClearingARangeThenSettingALaterStart_MovesTheStart()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 50));

        // Act — setting the later start alone would merge back into the allocation and change nothing
        var result = capacity.ChangeAllocations(
        [
            new AllocationChange(_teamA, null, Jan1, null, 0),
            new AllocationChange(_teamA, null, Feb1, null, 50),
        ], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Should().ContainSingle()
            .Which.ToDetails().Should().Be(new EmployeeAllocationDetails(_teamA, null, Feb1, null, 50));
        capacity.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void ChangeAllocations_WhenNothingChanges_RaisesNoEvent()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 50));

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamA, null, Feb1, Feb7, 50)], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeAllocations_WithSeveralChanges_AppliesThemTogether()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 100));

        // Act — either change alone is fine or not, but only the pair fits
        var result = capacity.ChangeAllocations(
        [
            new AllocationChange(_teamB, null, Feb1, Feb7, 50),
            new AllocationChange(_teamA, null, Feb1, Feb7, 50),
        ], EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Revision.Should().Be(2);
        capacity.DomainEvents.Should().ContainSingle();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ChangeAllocations_WithAPercentOutOfRange_Fails(int percent)
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamA, null, Jan1, null, percent)], EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        capacity.Allocations.Should().BeEmpty();
    }

    [Fact]
    public void ChangeAllocations_EndingBeforeItStarts_Fails()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.ChangeAllocations([new AllocationChange(_teamA, null, Feb7, Feb1, 50)], EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        capacity.Allocations.Should().BeEmpty();
    }

    [Fact]
    public void MoveAllocation_BetweenTeams_SucceedsWhereRaisingTheTargetFirstWouldNot()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 100));

        // Act
        var result = capacity.MoveAllocation(_teamA, null, _teamB, null, Feb1, Feb7, 50, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Select(a => a.ToDetails()).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, null, Jan1, Feb1.PlusDays(-1), 100),
            new EmployeeAllocationDetails(_teamA, null, Feb1, Feb7, 50),
            new EmployeeAllocationDetails(_teamB, null, Feb1, Feb7, 50),
            new EmployeeAllocationDetails(_teamA, null, Feb7.PlusDays(1), null, 100),
        ]);
        capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeAllocationsChangedEvent>();
    }

    [Fact]
    public void MoveAllocation_OntoATeamThatAlreadyHasSome_AddsToIt()
    {
        // Arrange
        var capacity = WithAllocations(
            new AllocationChange(_teamA, null, Jan1, null, 70),
            new AllocationChange(_teamB, null, Jan1, null, 30));

        // Act
        var result = capacity.MoveAllocation(_teamA, null, _teamB, null, Jan1, null, 20, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Select(a => a.ToDetails()).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, null, Jan1, null, 50),
            new EmployeeAllocationDetails(_teamB, null, Jan1, null, 50),
        ]);
    }

    [Fact]
    public void MoveAllocation_BetweenRolesOnOneTeam_Succeeds()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, _engineer, Jan1, null, 100));

        // Act
        var result = capacity.MoveAllocation(_teamA, _engineer, _teamA, _lead, Jan1, null, 30, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Select(a => a.ToDetails()).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, _engineer, Jan1, null, 70),
            new EmployeeAllocationDetails(_teamA, _lead, Jan1, null, 30),
        ]);
    }

    [Fact]
    public void MoveAllocation_OfMoreThanTheSourceHas_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 40));

        // Act
        var result = capacity.MoveAllocation(_teamA, null, _teamB, null, Feb1, Feb7, 50, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        capacity.Allocations.Should().ContainSingle();
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MoveAllocation_OverARangeTheSourceOnlyPartlyCovers_Fails()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Feb1, Feb7, 100));

        // Act
        var result = capacity.MoveAllocation(_teamA, null, _teamB, null, Jan1, Feb7, 50, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("2027-01-01");
    }

    [Fact]
    public void MoveAllocation_ToTheSameTeamAndRole_Fails()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 100));

        // Act
        var result = capacity.MoveAllocation(_teamA, null, _teamA, null, Jan1, null, 50, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void EndAllocations_EndsOnesRunningPastTheDayAndRemovesLaterOnes()
    {
        // Arrange
        var capacity = WithAllocations(
            new AllocationChange(_teamA, _engineer, Jan1, null, 60),
            new AllocationChange(_teamB, null, Jan1, Feb1.PlusDays(-1), 40),
            new AllocationChange(_teamB, null, Feb7, null, 40));

        // Act
        var result = capacity.EndAllocations(Feb1, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Select(a => a.ToDetails()).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, _engineer, Jan1, Feb1, 60),
            new EmployeeAllocationDetails(_teamB, null, Jan1, Feb1.PlusDays(-1), 40),
        ]);
        capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeAllocationsChangedEvent>();
    }

    [Fact]
    public void EndAllocations_WhenNothingRunsPastTheDay_RaisesNoEvent()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, Feb1, 60));

        // Act
        var result = capacity.EndAllocations(Feb1, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EndAllocationsTo_EndsTheTeamsAllocationsInEveryRoleAndLeavesOtherTeamsAlone()
    {
        // Arrange — team A in two roles, one open and one starting later, beside an open one on team B
        var capacity = WithAllocations(
            new AllocationChange(_teamA, _engineer, Jan1, null, 30),
            new AllocationChange(_teamA, _lead, Feb7, null, 20),
            new AllocationChange(_teamB, null, Jan1, null, 40));

        // Act
        var result = capacity.EndAllocationsTo(_teamA, Feb1, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Select(a => a.ToDetails()).Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, _engineer, Jan1, Feb1, 30),
            new EmployeeAllocationDetails(_teamB, null, Jan1, null, 40),
        ]);
        capacity.Revision.Should().Be(2);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeAllocationsChangedEvent>().Subject;
        raised.Added.Should().Equal(new EmployeeAllocationDetails(_teamA, _engineer, Jan1, Feb1, 30));
        raised.Removed.Should().BeEquivalentTo(
        [
            new EmployeeAllocationDetails(_teamA, _engineer, Jan1, null, 30),
            new EmployeeAllocationDetails(_teamA, _lead, Feb7, null, 20),
        ]);
        raised.Allocations.Should().HaveCount(2);
    }

    [Fact]
    public void EndAllocationsTo_KeepsAnAllocationThatEndsOnTheLastDay()
    {
        // Arrange
        var capacity = WithAllocations(
            new AllocationChange(_teamA, null, Jan1, Feb1, 60),
            new AllocationChange(_teamB, null, Jan1, null, 40));

        // Act
        var result = capacity.EndAllocationsTo(_teamA, Feb1, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Allocations.Should().HaveCount(2);
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EndAllocationsTo_FreesTheTimeFromTheNextDay()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 100));
        capacity.EndAllocationsTo(_teamA, Feb1, EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        var onTheLastDay = capacity.ChangeAllocations([new AllocationChange(_teamB, null, Feb1, null, 100)], EventActor.System, Now);
        var fromTheNextDay = capacity.ChangeAllocations([new AllocationChange(_teamB, null, Feb1.PlusDays(1), null, 100)], EventActor.System, Now);

        // Assert
        onTheLastDay.IsFailure.Should().BeTrue();
        fromTheNextDay.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EndAllocationsTo_WithNoTeam_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 60));

        // Act
        var result = capacity.EndAllocationsTo(Guid.Empty, Feb1, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An allocation needs a team.");
        capacity.Allocations.Should().ContainSingle().Which.End.Should().BeNull();
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeHolidayCalendar_SetsTheCalendarAndRaisesEventWithBothEnds()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        capacity.ChangeHolidayCalendar(first, EventActor.System, Now).IsSuccess.Should().BeTrue();
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.ChangeHolidayCalendar(second, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.HolidayCalendarId.Should().Be(second);
        capacity.Revision.Should().Be(2);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeHolidayCalendarChangedEvent>().Subject;
        raised.Id.Should().Be(capacity.Id);
        raised.PreviousHolidayCalendarId.Should().Be(first);
        raised.HolidayCalendarId.Should().Be(second);
        raised.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void ChangeHolidayCalendar_ToNone_ClearsTheCalendar()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        var calendarId = Guid.NewGuid();
        capacity.ChangeHolidayCalendar(calendarId, EventActor.System, Now);
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.ChangeHolidayCalendar(null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.HolidayCalendarId.Should().BeNull();
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeHolidayCalendarChangedEvent>().Subject;
        raised.PreviousHolidayCalendarId.Should().Be(calendarId);
        raised.HolidayCalendarId.Should().BeNull();
    }

    [Fact]
    public void ChangeHolidayCalendar_ToTheSameCalendar_ChangesNothing()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        var calendarId = Guid.NewGuid();
        capacity.ChangeHolidayCalendar(calendarId, EventActor.System, Now);
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.ChangeHolidayCalendar(calendarId, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeHolidayCalendar_WithAnEmptyId_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.ChangeHolidayCalendar(Guid.Empty, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        capacity.HolidayCalendarId.Should().BeNull();
        capacity.Revision.Should().Be(0);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeWorkingDayHours_SetsTheHoursAndRaisesEventWithBothEnds()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.ChangeWorkingDayHours(6m, EventActor.System, Now).IsSuccess.Should().BeTrue();
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.ChangeWorkingDayHours(4.5m, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.WorkingDayHours.Should().Be(4.5m);
        capacity.Revision.Should().Be(2);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeWorkingDayHoursChangedEvent>().Subject;
        raised.Id.Should().Be(capacity.Id);
        raised.PreviousWorkingDayHours.Should().Be(6m);
        raised.WorkingDayHours.Should().Be(4.5m);
        raised.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void ChangeWorkingDayHours_ToNone_GivesThemTheSystems()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.ChangeWorkingDayHours(4m, EventActor.System, Now);
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.ChangeWorkingDayHours(null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.WorkingDayHours.Should().BeNull();
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeWorkingDayHoursChangedEvent>().Subject;
        raised.PreviousWorkingDayHours.Should().Be(4m);
        raised.WorkingDayHours.Should().BeNull();
    }

    [Fact]
    public void ChangeWorkingDayHours_ToTheSameHours_ChangesNothing()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.ChangeWorkingDayHours(4m, EventActor.System, Now);
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.ChangeWorkingDayHours(4.00m, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("24.25")]
    [InlineData("4.125")]
    public void ChangeWorkingDayHours_OutsideTheHoursADayMayHave_FailsAndChangesNothing(string hours)
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.ChangeWorkingDayHours(decimal.Parse(hours, System.Globalization.CultureInfo.InvariantCulture), EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        capacity.WorkingDayHours.Should().BeNull();
        capacity.Revision.Should().Be(0);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordTimeAway_AddsTheTimeAwayAndRaisesEventWithoutTheReason()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.RecordTimeAway(Feb1, Feb7, 50, TimeAwayReason.Training, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var timeAway = capacity.TimeAway.Should().ContainSingle().Subject;
        result.Value.Should().BeSameAs(timeAway);
        timeAway.EmployeeId.Should().Be(capacity.Id);
        timeAway.Start.Should().Be(Feb1);
        timeAway.End.Should().Be(Feb7);
        timeAway.PercentAway.Should().Be(50);
        timeAway.Reason.Should().Be(TimeAwayReason.Training);
        capacity.Revision.Should().Be(1);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTimeAwayRecordedEvent>().Subject;
        raised.Id.Should().Be(capacity.Id);
        raised.TimeAwayId.Should().Be(timeAway.Id);
        raised.Start.Should().Be(Feb1);
        raised.End.Should().Be(Feb7);
        raised.PercentAway.Should().Be(50);
        raised.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void RecordTimeAway_NeverCountsTowardTheAllocationLimit()
    {
        // Arrange
        var capacity = WithAllocations(new AllocationChange(_teamA, null, Jan1, null, 100));

        // Act
        var away = capacity.RecordTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now);
        var stillFull = capacity.ChangeAllocations([new AllocationChange(_teamB, null, Feb1, Feb7, 1)], EventActor.System, Now);

        // Assert
        away.IsSuccess.Should().BeTrue();
        capacity.Allocations.Should().ContainSingle().Which.Percent.Should().Be(100);
        stillFull.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void RecordTimeAway_WithAShareOutsideOneToAHundred_FailsAndChangesNothing(int percentAway)
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.RecordTimeAway(Feb1, Feb7, percentAway, TimeAwayReason.Other, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Time away must be between 1% and 100% of a day.");
        capacity.TimeAway.Should().BeEmpty();
        capacity.Revision.Should().Be(0);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void RecordTimeAway_AtEitherEndOfTheShare_Succeeds(int percentAway)
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.RecordTimeAway(Feb1, Feb1, percentAway, TimeAwayReason.Other, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.TimeAway.Should().ContainSingle().Which.PercentAway.Should().Be(percentAway);
    }

    [Fact]
    public void RecordTimeAway_EndingBeforeItStarts_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.RecordTimeAway(Feb7, Feb1, 100, TimeAwayReason.Other, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Time away cannot end before it starts.");
        capacity.TimeAway.Should().BeEmpty();
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordTimeAway_WithAnUndefinedReason_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());

        // Act
        var result = capacity.RecordTimeAway(Feb1, Feb7, 100, (TimeAwayReason)0, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Time away needs a reason.");
        capacity.TimeAway.Should().BeEmpty();
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordTimeAway_OverlappingUpToAWholeDay_Succeeds()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 50, TimeAwayReason.Training);

        // Act
        var result = capacity.RecordTimeAway(Feb1.PlusDays(2), Feb7.PlusDays(2), 50, TimeAwayReason.PaidTimeOff, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.TimeAway.Should().HaveCount(2);
    }

    [Fact]
    public void RecordTimeAway_WhenADayPartWayThroughWouldBeMoreThanAWholeDay_FailsNamingThatDay()
    {
        // Arrange — the new stretch starts clear and runs into one that starts later
        var capacity = WithTimeAway(Feb1.PlusDays(3), Feb7, 50, TimeAwayReason.Training);

        // Act
        var result = capacity.RecordTimeAway(Feb1, Feb7, 51, TimeAwayReason.PaidTimeOff, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("101%").And.Contain("2027-02-04");
        capacity.TimeAway.Should().ContainSingle();
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RecordTimeAway_TheDayAfterAnotherStretchEnds_Succeeds()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff);

        // Act
        var onTheLastDay = capacity.RecordTimeAway(Feb7, Feb7.PlusDays(3), 1, TimeAwayReason.Other, EventActor.System, Now);
        var fromTheNextDay = capacity.RecordTimeAway(Feb7.PlusDays(1), Feb7.PlusDays(3), 100, TimeAwayReason.Other, EventActor.System, Now);

        // Assert
        onTheLastDay.IsFailure.Should().BeTrue();
        fromTheNextDay.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ChangeTimeAway_ChangesTheStretchAndRaisesEventWithBothEnds()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff);
        var timeAway = capacity.TimeAway.Single();

        // Act
        var result = capacity.ChangeTimeAway(timeAway.Id, Feb1.PlusDays(1), Feb7.PlusDays(1), 50, TimeAwayReason.PaidTimeOff, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        timeAway.Start.Should().Be(Feb1.PlusDays(1));
        timeAway.End.Should().Be(Feb7.PlusDays(1));
        timeAway.PercentAway.Should().Be(50);
        capacity.Revision.Should().Be(2);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTimeAwayChangedEvent>().Subject;
        raised.Id.Should().Be(capacity.Id);
        raised.TimeAwayId.Should().Be(timeAway.Id);
        raised.Start.Should().Be(Feb1.PlusDays(1));
        raised.End.Should().Be(Feb7.PlusDays(1));
        raised.PercentAway.Should().Be(50);
        raised.PreviousStart.Should().Be(Feb1);
        raised.PreviousEnd.Should().Be(Feb7);
        raised.PreviousPercentAway.Should().Be(100);
        raised.ReasonChanged.Should().BeFalse();
    }

    [Fact]
    public void ChangeTimeAway_OfTheReasonOnly_SaysItChangedWithoutMovingAnything()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff);
        var timeAway = capacity.TimeAway.Single();

        // Act
        var result = capacity.ChangeTimeAway(timeAway.Id, Feb1, Feb7, 100, TimeAwayReason.BusinessTravel, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        timeAway.Reason.Should().Be(TimeAwayReason.BusinessTravel);
        capacity.Revision.Should().Be(2);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTimeAwayChangedEvent>().Subject;
        raised.ReasonChanged.Should().BeTrue();
        raised.Start.Should().Be(raised.PreviousStart);
        raised.End.Should().Be(raised.PreviousEnd);
        raised.PercentAway.Should().Be(raised.PreviousPercentAway);
    }

    [Fact]
    public void ChangeTimeAway_ToWhatItAlreadyIs_ChangesNothing()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff);

        // Act
        var result = capacity.ChangeTimeAway(capacity.TimeAway.Single().Id, Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeTimeAway_LeavesItsOwnShareOutOfTheDayTotal()
    {
        // Arrange — a whole day away can be moved over days it already covers
        var capacity = WithTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff);

        // Act
        var result = capacity.ChangeTimeAway(capacity.TimeAway.Single().Id, Feb1.PlusDays(2), Feb7.PlusDays(2), 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.TimeAway.Single().Start.Should().Be(Feb1.PlusDays(2));
    }

    [Fact]
    public void ChangeTimeAway_WhenADayWouldBeMoreThanAWholeDay_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb1, 60, TimeAwayReason.Training);
        var moving = capacity.RecordTimeAway(Feb7, Feb7, 60, TimeAwayReason.PaidTimeOff, EventActor.System, Now).Value;
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.ChangeTimeAway(moving.Id, Feb1, Feb1, 60, TimeAwayReason.PaidTimeOff, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("120%").And.Contain("2027-02-01");
        moving.Start.Should().Be(Feb7);
        capacity.Revision.Should().Be(2);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeTimeAway_ThatIsNotTheEmployees_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff);
        var unknown = Guid.NewGuid();

        // Act
        var result = capacity.ChangeTimeAway(unknown, Feb1, Feb1, 50, TimeAwayReason.Other, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Time away {unknown} is not this employee's.");
        capacity.TimeAway.Single().PercentAway.Should().Be(100);
        capacity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTimeAway_RemovesTheStretchAndRaisesEvent()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 50, TimeAwayReason.Training);
        var kept = capacity.RecordTimeAway(Jan1, Jan1, 100, TimeAwayReason.PaidTimeOff, EventActor.System, Now).Value;
        var removing = capacity.TimeAway.Single(t => t.Id != kept.Id);
        capacity.ClearDomainEvents();

        // Act
        var result = capacity.RemoveTimeAway(removing.Id, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capacity.TimeAway.Should().ContainSingle().Which.Should().BeSameAs(kept);
        capacity.Revision.Should().Be(3);
        var raised = capacity.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeTimeAwayRemovedEvent>().Subject;
        raised.Id.Should().Be(capacity.Id);
        raised.TimeAwayId.Should().Be(removing.Id);
        raised.Start.Should().Be(Feb1);
        raised.End.Should().Be(Feb7);
        raised.PercentAway.Should().Be(50);
    }

    [Fact]
    public void RemoveTimeAway_ThatIsNotTheEmployees_FailsAndChangesNothing()
    {
        // Arrange
        var capacity = WithTimeAway(Feb1, Feb7, 100, TimeAwayReason.PaidTimeOff);

        // Act
        var result = capacity.RemoveTimeAway(Guid.NewGuid(), EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        capacity.TimeAway.Should().ContainSingle();
        capacity.Revision.Should().Be(1);
        capacity.DomainEvents.Should().BeEmpty();
    }

    private static EmployeeCapacity WithTimeAway(LocalDate start, LocalDate end, int percentAway, TimeAwayReason reason)
    {
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.RecordTimeAway(start, end, percentAway, reason, EventActor.System, Now).IsSuccess.Should().BeTrue();
        capacity.ClearDomainEvents();
        return capacity;
    }

    private static EmployeeCapacity WithAllocations(params AllocationChange[] changes)
    {
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.ChangeAllocations(changes, EventActor.System, Now).IsSuccess.Should().BeTrue();
        capacity.ClearDomainEvents();
        return capacity;
    }
}
