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
        copy.Watermarks.Record.Should().Be(Created);
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
    public async Task Handle_Updated_WhenNewerThanTheCopy_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithName("Old Name").WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(UpdatedEvent(id, "New Name", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).Name.Should().Be("New Name");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenDeliveredOutOfOrder_KeepsTheLaterEdit()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIteration(new WorkIterationFaker().WithId(id).WithDateRange(Range).Generate(), Created));

        // Act
        await _handler.Handle(UpdatedEvent(id, "Sprint 1b", SecondEdit), TestContext.Current.CancellationToken);
        await _handler.Handle(UpdatedEvent(id, "Sprint 1a", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).Name.Should().Be("Sprint 1b");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange
        var source = new WorkIterationFaker().WithName("New Name").WithDateRange(Range).Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(UpdatedEvent(source.Id, "New Name", FirstEdit), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().ContainSingle(i => i.Id == source.Id).Which.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_Updated_WhenDeliveredAfterTheIterationWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(UpdatedEvent(Guid.CreateVersion7(), "Whatever", FirstEdit), TestContext.Current.CancellationToken);

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

    private static IterationUpdatedEvent UpdatedEvent(Guid id, string name, Instant timestamp) =>
        new(
            id: id,
            key: 1,
            name: name,
            type: IterationType.Iteration,
            state: IterationState.Active,
            dateRange: Range,
            teamId: null,
            actor: EventActor.System,
            timestamp: timestamp);
}
