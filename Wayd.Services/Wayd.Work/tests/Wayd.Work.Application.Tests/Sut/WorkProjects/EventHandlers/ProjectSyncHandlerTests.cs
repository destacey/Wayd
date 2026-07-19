using FluentAssertions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProjects.EventHandlers;
using Wayd.Work.Domain.Tests.Data;
using Moq;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProjects.EventHandlers;

/// <summary>
/// <see cref="ProjectSyncHandler"/> replicates PPM Project changes into the Work <c>WorkProject</c>
/// projection. Because it is delivered durably, its contract is: idempotent guards short-circuit a
/// redelivery, and any real failure PROPAGATES (Wolverine's retry/dead-letter governs it) rather than being
/// swallowed.
/// </summary>
public sealed class ProjectSyncHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly ProjectSyncHandler _handler;

    public ProjectSyncHandlerTests()
    {
        _handler = new ProjectSyncHandler(_workDbContext, Mock.Of<ILogger<ProjectSyncHandler>>());
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenProjectIsNew_AddsWorkProjectAndSaves()
    {
        // Arrange
        var @event = CreatedEvent(Guid.CreateVersion7(), "NEW01", "New Project");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().ContainSingle(p => p.Id == @event.Id);
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenProjectAlreadyExists_IsIdempotentNoOp()
    {
        // Arrange — a redelivery, or a race with the Hangfire bulk sync, finds the projection already there.
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProjectFaker().WithId(id).Generate());

        var @event = CreatedEvent(id, "DUP01", "Duplicate Project");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert — no second row, no save.
        _workDbContext.WorkProjects.Should().ContainSingle(p => p.Id == id);
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Updated_WhenProjectExists_UpdatesDetailsAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProjectFaker().WithId(id).WithName("Old Name").Generate());

        var @event = UpdatedEvent(id, "UPD01", "New Name");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Single(p => p.Id == id).Name.Should().Be("New Name");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenProjectMissing_IsNoOp()
    {
        // Arrange — out-of-order delivery: the update arrives before the create was applied.
        var @event = UpdatedEvent(Guid.CreateVersion7(), "UPD02", "Whatever");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert — nothing to update, nothing saved.
        _workDbContext.WorkProjects.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Deleted_WhenProjectExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProjectFaker().WithId(id).Generate());

        var @event = new ProjectDeletedEvent(id, Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenProjectMissing_IsIdempotentNoOp()
    {
        // Arrange — a redelivery of a delete that already ran; the goal state (absent) already holds.
        var @event = new ProjectDeletedEvent(Guid.CreateVersion7(), Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static ProjectCreatedEvent CreatedEvent(Guid id, string key, string name) =>
        new(
            id: id,
            key: new ProjectKey(key),
            name: name,
            description: "desc",
            expenditureCategoryId: 1,
            statusId: 1,
            dateRange: null,
            portfolioId: Guid.CreateVersion7(),
            programId: null,
            roles: null,
            strategicThemes: [],
            timestamp: Now);

    private static ProjectDetailsUpdatedEvent UpdatedEvent(Guid id, string key, string name) =>
        new(
            id: id,
            key: new ProjectKey(key),
            name: name,
            description: "desc",
            expenditureCategoryId: 1,
            timestamp: Now);
}
