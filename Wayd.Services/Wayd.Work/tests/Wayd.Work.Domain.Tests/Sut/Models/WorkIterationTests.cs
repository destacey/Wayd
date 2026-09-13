using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.WorkManagement.WorkIterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkIterationTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 4, 12, 12, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void ApplyRecord_WhenValuesChange_RaisesUpdatedEvent()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithState(IterationState.Future).Generate(), Created);
        var source = SameIterationAs(iteration).WithState(IterationState.Active).Generate();

        // Act
        var applied = iteration.ApplyRecord(source, EventActor.System, Later);

        // Assert
        applied.Should().BeTrue();
        iteration.State.Should().Be(IterationState.Active);
        iteration.Watermarks.Record.Should().Be(Later);
        iteration.DomainEvents.Should().ContainSingle(e => e is WorkIterationUpdatedEvent);
    }

    [Fact]
    public void ApplyRecord_WhenNothingChangedButNewer_AdvancesTheWatermarkWithoutAnEvent()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().Generate(), Created);
        var source = SameIterationAs(iteration).Generate();

        // Act
        var applied = iteration.ApplyRecord(source, EventActor.System, Later);

        // Assert
        applied.Should().BeTrue();
        iteration.Watermarks.Record.Should().Be(Later);
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyRecord_WhenOlderThanTheLastChange_IsSkippedWithoutAnEvent()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 2").Generate(), Created);
        var source = SameIterationAs(iteration).WithName("Sprint 1").Generate();

        // Act
        var applied = iteration.ApplyRecord(source, EventActor.System, Earlier);

        // Assert
        applied.Should().BeFalse();
        iteration.Name.Should().Be("Sprint 2");
        iteration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyRecord_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 1").Generate(), Created);
        var source = SameIterationAs(iteration).WithName("Sprint 2").Generate();
        iteration.ApplyRecord(source, EventActor.System, Later);
        iteration.ClearDomainEvents();

        // Act
        var applied = iteration.ApplyRecord(source, EventActor.System, Later);

        // Assert
        applied.Should().BeFalse();
        iteration.DomainEvents.Should().BeEmpty();
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
    public void Resync_WhenTheCopyAlreadyMatches_KeepsItsWatermark()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().Generate(), Created);
        var source = SameIterationAs(iteration).Generate();

        // Act
        var changed = iteration.Resync(source, EventActor.System, Later);

        // Assert
        changed.Should().BeFalse();
        iteration.Watermarks.Record.Should().Be(Created);
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsAChangeNewerThanTheRead_LeavesItAlone()
    {
        // Arrange
        var iteration = new WorkIteration(new WorkIterationFaker().WithName("Sprint 1").Generate(), Earlier);
        iteration.ApplyRecord(SameIterationAs(iteration).WithName("Sprint 2").Generate(), EventActor.System, Later);
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
