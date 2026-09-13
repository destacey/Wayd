using FluentAssertions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Planning.Iterations;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkIterations.EventHandlers;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Moq;
using Xunit;
using Wayd.Common.Domain.Events;

namespace Wayd.Work.Application.Tests.Sut.WorkIterations.EventHandlers;

/// <summary>
/// <see cref="WorkIterationSyncHandler"/> keeps the Work copy of each iteration correct however the durable
/// <c>Iteration*</c> events arrive: late, twice, out of order, or after the iteration was deleted.
/// </summary>
public sealed class WorkIterationSyncHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant FirstEdit = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant SecondEdit = Created.Plus(Duration.FromMinutes(10));
    private static readonly IterationDateRange Range =
        new(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0));

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly WorkIterationSyncHandler _handler;

    public WorkIterationSyncHandlerTests()
    {
        _handler = new WorkIterationSyncHandler(_workDbContext, _dispatcher.Object, Mock.Of<ILogger<WorkIterationSyncHandler>>());
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new WorkIterationFaker().WithName("Sprint 1").WithDateRange(Range).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _workDbContext.WorkIterations.Should().ContainSingle(i => i.Id == source.Id).Subject;
        copy.Name.Should().Be("Sprint 1");
        copy.Watermarks.Should().Be(WorkIterationWatermarks.At(Created));
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(CreatedEvent(id), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().ContainSingle(i => i.Id == id);
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheIterationWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithName("Old Name").WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "New Name", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).Name.Should().Be("New Name");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenDeliveredOutOfOrder_KeepsTheLaterEdit()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "Sprint 1b", SecondEdit), TestContext.Current.CancellationToken);
        await _handler.Handle(DetailsUpdatedEvent(id, "Sprint 1a", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).Name.Should().Be("Sprint 1b");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_StateChangedAndDetailsUpdated_WhenDeliveredOutOfOrder_ApplyBoth()
    {
        // Arrange — a state change at FirstEdit arrives after a rename at SecondEdit; neither replaces the other.
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithState(IterationState.Future).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "Renamed", SecondEdit), TestContext.Current.CancellationToken);
        await _handler.Handle(new IterationStateChangedEvent(id, 1, IterationState.Future, IterationState.Active, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        var copy = _workDbContext.WorkIterations.Single(i => i.Id == id);
        copy.Name.Should().Be("Renamed");
        copy.State.Should().Be(IterationState.Active);
    }

    [Fact]
    public async Task Handle_DateRangeChanged_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var moved = new IterationDateRange(Range.Start, Instant.FromUtc(2026, 1, 21, 0, 0));
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(new IterationDateRangeChangedEvent(id, 1, Range, moved, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).DateRange.Should().Be(moved);
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TeamChanged_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        var teamId = Guid.NewGuid();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithTeamId(null).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(new IterationTeamChangedEvent(id, 1, null, teamId, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).TeamId.Should().Be(teamId);
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SupersededUpdated_AppliesEveryGroupItCarries()
    {
        // Arrange — an envelope written as the superseded type before the switch, still in the outbox.
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithName("Old Name").WithState(IterationState.Future).WithTeamId(null).WithDateRange(Range).Generate(), Created));

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        await _handler.Handle(new IterationUpdatedEvent(id, 1, "New Name", IterationType.Iteration, IterationState.Active, Range, null, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);
#pragma warning restore CS0618

        // Assert
        var copy = _workDbContext.WorkIterations.Single(i => i.Id == id);
        copy.Name.Should().Be("New Name");
        copy.State.Should().Be(IterationState.Active);
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange
        var source = new WorkIterationFaker().WithName("New Name").WithDateRange(Range).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, "New Name", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        var copy = _workDbContext.WorkIterations.Should().ContainSingle(i => i.Id == source.Id).Subject;
        copy.Name.Should().Be("New Name");
        copy.Watermarks.Should().Be(WorkIterationWatermarks.At(FirstEdit));
    }

    [Fact]
    public async Task Handle_StateChanged_WhenDeliveredAfterTheIterationWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(new IterationStateChangedEvent(Guid.CreateVersion7(), 1, IterationState.Future, IterationState.Active, EventActor.System, FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(new IterationDeletedEvent(id, EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(new IterationDeletedEvent(Guid.CreateVersion7(), EventActor.System, SecondEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    private void SourceReturns(ISimpleIteration? iteration) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleIterationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(iteration);

    private static IterationCreatedEvent CreatedEvent(Guid id) =>
        new(
            id: id,
            key: 1,
            name: "Sprint 1",
            type: IterationType.Iteration,
            state: IterationState.Active,
            dateRange: Range,
            teamId: null,
            actor: EventActor.System,
            timestamp: Created);

    private static IterationDetailsUpdatedEvent DetailsUpdatedEvent(Guid id, string name, Instant timestamp) =>
        new(id, 1, name, IterationType.Iteration, new IterationDetails("Sprint 1", IterationType.Iteration), EventActor.System, timestamp);
}
