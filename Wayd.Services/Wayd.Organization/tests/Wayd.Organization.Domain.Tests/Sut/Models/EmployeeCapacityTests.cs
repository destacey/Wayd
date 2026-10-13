using NodaTime;
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

    private static EmployeeCapacity WithAllocations(params AllocationChange[] changes)
    {
        var capacity = EmployeeCapacity.Create(Guid.NewGuid());
        capacity.ChangeAllocations(changes, EventActor.System, Now).IsSuccess.Should().BeTrue();
        capacity.ClearDomainEvents();
        return capacity;
    }
}
