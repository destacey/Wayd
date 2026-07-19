using FluentAssertions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkIterations.EventHandlers;
using Wayd.Work.Domain.Tests.Data;
using Moq;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkIterations.EventHandlers;

/// <summary>
/// <see cref="WorkIterationSyncHandler"/> replicates Planning Iteration changes into the Work
/// <c>WorkIteration</c> projection. Because it is delivered durably, its contract is: idempotent guards
/// short-circuit a redelivery, and any real failure PROPAGATES (Wolverine's retry/dead-letter governs it)
/// rather than being swallowed.
/// </summary>
public sealed class WorkIterationSyncHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);
    private static readonly IterationDateRange Range =
        new(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0));

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly WorkIterationSyncHandler _handler;

    public WorkIterationSyncHandlerTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);
        _handler = new WorkIterationSyncHandler(_workDbContext, Mock.Of<ILogger<WorkIterationSyncHandler>>(), dateTimeProvider.Object);
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenIterationIsNew_AddsProjectionAndSaves()
    {
        // Arrange
        var @event = CreatedEvent(Guid.CreateVersion7(), 1, "Sprint 1");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().ContainSingle(i => i.Id == @event.Id);
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenIterationAlreadyExists_IsIdempotentNoOp()
    {
        // Arrange — a redelivery, or a race with the Hangfire bulk sync, finds the projection already there.
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIterationFaker().WithId(id).WithDateRange(Range).Generate());

        var @event = CreatedEvent(id, 2, "Duplicate");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert — no second row, no save.
        _workDbContext.WorkIterations.Should().ContainSingle(i => i.Id == id);
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Updated_WhenIterationExists_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIterationFaker().WithId(id).WithName("Old Name").WithDateRange(Range).Generate());

        var @event = UpdatedEvent(id, 3, "New Name");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Single(i => i.Id == id).Name.Should().Be("New Name");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenIterationMissing_IsNoOp()
    {
        // Arrange — out-of-order delivery: the update arrives before the create was applied.
        var @event = UpdatedEvent(Guid.CreateVersion7(), 4, "Whatever");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Deleted_WhenIterationExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkIteration(new WorkIterationFaker().WithId(id).WithDateRange(Range).Generate());

        var @event = new IterationDeletedEvent(id, Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkIterations.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenIterationMissing_IsIdempotentNoOp()
    {
        // Arrange — a redelivery of a delete that already ran; the goal state (absent) already holds.
        var @event = new IterationDeletedEvent(Guid.CreateVersion7(), Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static IterationCreatedEvent CreatedEvent(Guid id, int key, string name) =>
        new(
            id: id,
            key: key,
            name: name,
            type: IterationType.Iteration,
            state: IterationState.Active,
            dateRange: Range,
            teamId: null,
            timestamp: Now);

    private static IterationUpdatedEvent UpdatedEvent(Guid id, int key, string name) =>
        new(
            id: id,
            key: key,
            name: name,
            type: IterationType.Iteration,
            state: IterationState.Active,
            dateRange: Range,
            teamId: null,
            timestamp: Now);
}
