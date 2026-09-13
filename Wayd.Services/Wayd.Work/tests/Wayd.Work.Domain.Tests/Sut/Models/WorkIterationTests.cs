using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.WorkManagement.WorkIterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkIterationTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 4, 12, 12, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void ApplyDetails_WhenValuesChange_RaisesEventCarryingBothEnds()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 1").WithType(IterationType.Iteration).Generate(), Created);

        // Act
        var applied = iteration.ApplyDetails("Sprint 1a", IterationType.Sprint, EventActor.System, Later);

        // Assert
        applied.Should().BeTrue();
        iteration.Name.Should().Be("Sprint 1a");
        iteration.Watermarks.Details.Should().Be(Later);
        iteration.Watermarks.State.Should().Be(Created);
        var raised = iteration.DomainEvents.OfType<WorkIterationDetailsUpdatedEvent>().Should().ContainSingle().Subject;
        raised.Key.Should().Be(iteration.Key);
        raised.Name.Should().Be("Sprint 1a");
        raised.Type.Should().Be(IterationType.Sprint);
        raised.Previous.Should().Be(new IterationDetails("Sprint 1", IterationType.Iteration));
    }

    [Fact]
    public void ApplyDateRange_WhenTheRangeMoves_RaisesEventCarryingBothEnds()
    {
        // Arrange
        var before = new IterationDateRange(Instant.FromUtc(2026, 4, 1, 0, 0), Instant.FromUtc(2026, 4, 14, 0, 0));
        var after = new IterationDateRange(Instant.FromUtc(2026, 4, 1, 0, 0), Instant.FromUtc(2026, 4, 21, 0, 0));
        var iteration = new WorkIteration(new WorkIterationFaker().WithDateRange(before).Generate(), Created);

        // Act
        var applied = iteration.ApplyDateRange(after, EventActor.System, Later);

        // Assert
        applied.Should().BeTrue();
        iteration.DateRange.Should().Be(after);
        var raised = iteration.DomainEvents.OfType<WorkIterationDateRangeChangedEvent>().Should().ContainSingle().Subject;
        raised.PreviousDateRange.Should().Be(before);
        raised.DateRange.Should().Be(after);
    }

    [Fact]
    public void ApplyState_WhenTheStateMoves_RaisesEventCarryingBothEnds()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithState(IterationState.Future).Generate(), Created);

        // Act
        var applied = iteration.ApplyState(IterationState.Active, EventActor.System, Later);

        // Assert
        applied.Should().BeTrue();
        iteration.State.Should().Be(IterationState.Active);
        var raised = iteration.DomainEvents.OfType<WorkIterationStateChangedEvent>().Should().ContainSingle().Subject;
        raised.FromState.Should().Be(IterationState.Future);
        raised.ToState.Should().Be(IterationState.Active);
    }

    [Fact]
    public void ApplyTeam_WhenTheTeamChanges_RaisesEventCarryingBothEnds()
    {
        // Arrange
        var previousTeamId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var iteration = new WorkIteration(new WorkIterationFaker().WithTeamId(previousTeamId).Generate(), Created);

        // Act
        var applied = iteration.ApplyTeam(teamId, EventActor.System, Later);

        // Assert
        applied.Should().BeTrue();
        iteration.TeamId.Should().Be(teamId);
        var raised = iteration.DomainEvents.OfType<WorkIterationTeamChangedEvent>().Should().ContainSingle().Subject;
        raised.PreviousTeamId.Should().Be(previousTeamId);
        raised.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void ApplyState_WhenNothingChangedButNewer_AdvancesTheWatermarkWithoutAnEvent()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithState(IterationState.Active).Generate(), Created);

        // Act
        var applied = iteration.ApplyState(IterationState.Active, EventActor.System, Later);

        // Assert
        applied.Should().BeTrue();
        iteration.Watermarks.State.Should().Be(Later);
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyDetails_WhenOlderThanTheLastChange_IsSkippedWithoutAnEvent()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 2").Generate(), Created);

        // Act
        var applied = iteration.ApplyDetails("Sprint 1", iteration.Type, EventActor.System, Earlier);

        // Assert
        applied.Should().BeFalse();
        iteration.Name.Should().Be("Sprint 2");
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyState_WhenAnotherGroupTookANewerChange_StillApplies()
    {
        // Arrange — renamed at Later; a state change from Created's minute after it arrives afterwards.
        var iteration = new WorkIteration(new WorkIterationFaker().WithState(IterationState.Future).Generate(), Earlier);
        iteration.ApplyDetails("Renamed", iteration.Type, EventActor.System, Later);

        // Act
        var applied = iteration.ApplyState(IterationState.Active, EventActor.System, Created);

        // Assert
        applied.Should().BeTrue();
        iteration.State.Should().Be(IterationState.Active);
    }

    [Fact]
    public void ApplyDetails_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 1").Generate(), Created);
        iteration.ApplyDetails("Sprint 2", iteration.Type, EventActor.System, Later);
        iteration.ClearDomainEvents();

        // Act
        var applied = iteration.ApplyDetails("Sprint 2", iteration.Type, EventActor.System, Later);

        // Assert
        applied.Should().BeFalse();
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyRecord_AppliesEachGroupAgainstItsOwnWatermark()
    {
        // Arrange — the state took a change at Later; a whole record from Created renames and moves the state.
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 1").WithState(IterationState.Future).Generate(), Earlier);
        iteration.ApplyState(IterationState.Completed, EventActor.System, Later);
        iteration.ClearDomainEvents();
        var record = SameIterationAs(iteration).WithName("Sprint 1a").WithState(IterationState.Active).Generate();

        // Act
        var applied = iteration.ApplyRecord(record, EventActor.System, Created);

        // Assert
        applied.Should().BeTrue();
        iteration.Name.Should().Be("Sprint 1a");
        iteration.State.Should().Be(IterationState.Completed);
        iteration.DomainEvents.Should().ContainSingle(e => e is WorkIterationDetailsUpdatedEvent);
    }

    [Fact]
    public void ApplyRecord_WithADifferentIteration_Throws()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().Generate(), Created);
        var other = new WorkIterationFaker().Generate();

        // Act
        var act = () => iteration.ApplyRecord(other, EventActor.System, Later);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Resync_WhenTheCopyAlreadyMatches_KeepsItsWatermarks()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().Generate(), Created);
        var source = SameIterationAs(iteration).Generate();

        // Act
        var changed = iteration.Resync(source, EventActor.System, Later);

        // Assert
        changed.Should().BeFalse();
        iteration.Watermarks.Should().Be(WorkIterationWatermarks.At(Created));
    }

    [Fact]
    public void Resync_StampsOnlyTheGroupsThatDiffer()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithState(IterationState.Future).Generate(), Created);
        var source = SameIterationAs(iteration).WithState(IterationState.Active).Generate();

        // Act
        var changed = iteration.Resync(source, EventActor.System, Later);

        // Assert
        changed.Should().BeTrue();
        iteration.Watermarks.Should().Be(WorkIterationWatermarks.At(Created) with { State = Later });
        iteration.DomainEvents.Should().ContainSingle(e => e is WorkIterationStateChangedEvent);
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsAChangeNewerThanTheRead_LeavesThatGroupAlone()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 1").Generate(), Earlier);
        iteration.ApplyDetails("Sprint 2", iteration.Type, EventActor.System, Later);
        iteration.ClearDomainEvents();
        var readBeforeTheRename = SameIterationAs(iteration).WithName("Sprint 1").Generate();

        // Act
        var changed = iteration.Resync(readBeforeTheRename, EventActor.System, Created);

        // Assert
        changed.Should().BeFalse();
        iteration.Name.Should().Be("Sprint 2");
        iteration.DomainEvents.Should().BeEmpty();
    }

    private static WorkIterationFaker SameIterationAs(WorkIteration iteration) =>
        new WorkIterationFaker()
            .WithId(iteration.Id)
            .WithKey(iteration.Key)
            .WithName(iteration.Name)
            .WithType(iteration.Type)
            .WithState(iteration.State)
            .WithDateRange(iteration.DateRange)
            .WithTeamId(iteration.TeamId);
}
