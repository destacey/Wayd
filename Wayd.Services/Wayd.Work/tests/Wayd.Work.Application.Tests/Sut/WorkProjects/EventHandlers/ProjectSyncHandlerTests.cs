using FluentAssertions;
using Microsoft.Extensions.Logging;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProjects.EventHandlers;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Moq;
using Xunit;
using Wayd.Common.Domain.Events;

namespace Wayd.Work.Application.Tests.Sut.WorkProjects.EventHandlers;

/// <summary>
/// <see cref="ProjectSyncHandler"/> keeps the Work copy of each project correct however the durable
/// <c>Project*</c> events arrive: late, twice, out of order, or after the project was deleted.
/// </summary>
public sealed class ProjectSyncHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Edited = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant Rekeyed = Created.Plus(Duration.FromMinutes(10));

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly ProjectSyncHandler _handler;

    public ProjectSyncHandlerTests()
    {
        _handler = new ProjectSyncHandler(_workDbContext, _dispatcher.Object, Mock.Of<ILogger<ProjectSyncHandler>>());
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new WorkProjectFaker().WithName("Atlas").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _workDbContext.WorkProjects.Should().ContainSingle(p => p.Id == source.Id).Subject;
        copy.Name.Should().Be("Atlas");
        copy.Watermarks.Should().Be(WorkProjectWatermarks.At(Created));
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange — a redelivery, or a race with the Hangfire bulk sync, finds the copy already there.
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProject(new WorkProjectFaker().WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(CreatedEvent(id), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().ContainSingle(p => p.Id == id);
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheProjectWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenNewerThanTheCopy_UpdatesDetailsAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProject(new WorkProjectFaker().WithId(id).WithName("Old Name").Generate(), Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "OLDKEY", "New Name", Edited), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Single(p => p.Id == id).Name.Should().Be("New Name");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange — the create is still in flight, and the source already holds the edit.
        var source = new WorkProjectFaker().WithName("New Name").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, source.Key.Value, "New Name", Edited), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().ContainSingle(p => p.Id == source.Id).Which.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_DetailsUpdated_DeliveredAfterARekey_DoesNotRestoreTheOldKey()
    {
        // Arrange — the details payload still carries the key from before the rekey.
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProject(new WorkProjectFaker().WithId(id).WithKey(new ProjectKey("OLDKEY")).Generate(), Created));
        await _handler.Handle(KeyChangedEvent(id, "NEWKEY", Rekeyed), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, "OLDKEY", "Atlas", Edited), TestContext.Current.CancellationToken);

        // Assert
        var copy = _workDbContext.WorkProjects.Single(p => p.Id == id);
        copy.Key.Value.Should().Be("NEWKEY");
        copy.Name.Should().Be("Atlas");
    }

    [Fact]
    public async Task Handle_KeyChanged_WhenOlderThanTheCopy_IsSkipped()
    {
        // Arrange — rekeyed twice, delivered newest first.
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProject(new WorkProjectFaker().WithId(id).WithKey(new ProjectKey("FIRST")).Generate(), Created));
        await _handler.Handle(KeyChangedEvent(id, "THIRD", Rekeyed), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(KeyChangedEvent(id, "SECOND", Edited), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Single(p => p.Id == id).Key.Value.Should().Be("THIRD");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SupersededKeyChanged_StillInTheOutbox_StillRekeysTheCopy()
    {
        // Arrange — an envelope written as the superseded type before the switch to V2
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProject(new WorkProjectFaker().WithId(id).WithKey(new ProjectKey("OLDKEY")).Generate(), Created));

#pragma warning disable CS0618 // the superseded type is exactly what is under test
        var @event = new ProjectKeyChangedEvent(id, new ProjectKey("NEWKEY"), "Atlas", EventActor.System, Rekeyed);
#pragma warning restore CS0618

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Single(p => p.Id == id).Key.Value.Should().Be("NEWKEY");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_KeyChanged_WhenDeliveredAfterTheProjectWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(KeyChangedEvent(Guid.CreateVersion7(), "GHOST1", Rekeyed), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _workDbContext.AddWorkProject(new WorkProject(new WorkProjectFaker().WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(new ProjectDeletedEvent(id, EventActor.System, Rekeyed), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkProjects.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(new ProjectDeletedEvent(Guid.CreateVersion7(), EventActor.System, Rekeyed), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    private void SourceReturns(ISimpleProject? project) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleProjectQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

    private static ProjectCreatedEvent CreatedEvent(Guid id) =>
        new(
            id: id,
            key: new ProjectKey("NEW01"),
            name: "Atlas",
            description: "desc",
            expenditureCategoryId: 1,
            statusId: 1,
            dateRange: null,
            portfolioId: Guid.CreateVersion7(),
            programId: null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: [],
            actor: EventActor.System,
            timestamp: Created);

    private static ProjectDetailsUpdatedEvent DetailsUpdatedEvent(Guid id, string key, string name, Instant timestamp) =>
        new(
            id: id,
            key: new ProjectKey(key),
            name: name,
            description: "desc",
            expenditureCategoryId: 1,
            businessCase: null,
            expectedBenefits: null,
            previous: null,
            actor: EventActor.System,
            timestamp: timestamp);

    private static ProjectKeyChangedEventV2 KeyChangedEvent(Guid id, string key, Instant timestamp) =>
        new(
            id: id,
            previousKey: new ProjectKey("OLDKEY"),
            key: new ProjectKey(key),
            actor: EventActor.System,
            timestamp: timestamp);
}
