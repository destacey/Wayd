using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.DemandManagement.Application.DemandProjects.EventHandlers;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.DemandProjects.EventHandlers;

/// <summary>
/// <see cref="DemandProjectSyncHandler"/> keeps Demand Management's copy of each project correct however the
/// durable <c>Project*</c> events arrive: late, twice, out of order, or after the project was deleted.
/// </summary>
public sealed class DemandProjectSyncHandlerTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Edited = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant Rekeyed = Created.Plus(Duration.FromMinutes(10));

    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly DemandProjectSyncHandler _handler;

    public DemandProjectSyncHandlerTests()
    {
        _handler = new DemandProjectSyncHandler(_dbContext, _dispatcher.Object, Mock.Of<ILogger<DemandProjectSyncHandler>>());
    }

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = SourceProject.Any("ATLAS", "Atlas");
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandProjects.Should().ContainSingle(p => p.Id == source.Id).Subject;
        copy.Name.Should().Be("Atlas");
        copy.Key.Value.Should().Be("ATLAS");
        copy.Watermarks.Should().Be(DemandProjectWatermarks.At(Created));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var source = SourceProject.Any();
        _dbContext.AddProjects(new DemandProject(source, Created));

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheProjectWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenNewerThanTheCopy_RenamesAndSaves()
    {
        // Arrange
        var source = SourceProject.Any(name: "Old Name");
        _dbContext.AddProjects(new DemandProject(source, Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, source.Key.Value, "New Name", Edited), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Single().Name.Should().Be("New Name");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange — the create is still in flight, and the source already holds the edit.
        var source = SourceProject.Any(name: "New Name");
        SourceReturns(source);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, source.Key.Value, "New Name", Edited), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().ContainSingle(p => p.Id == source.Id).Which.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_DetailsUpdated_DeliveredAfterARekey_DoesNotRestoreTheOldKey()
    {
        // Arrange — the details payload still carries the key from before the rekey.
        var source = SourceProject.Any("OLDKEY");
        _dbContext.AddProjects(new DemandProject(source, Created));
        await _handler.Handle(KeyChangedEvent(source.Id, "NEWKEY", Rekeyed), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, "OLDKEY", "Atlas", Edited), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandProjects.Single();
        copy.Key.Value.Should().Be("NEWKEY");
        copy.Name.Should().Be("Atlas");
    }

    [Fact]
    public async Task Handle_KeyChanged_WhenOlderThanTheCopy_IsSkipped()
    {
        // Arrange — rekeyed twice, delivered newest first.
        var source = SourceProject.Any("FIRST");
        _dbContext.AddProjects(new DemandProject(source, Created));
        await _handler.Handle(KeyChangedEvent(source.Id, "THIRD", Rekeyed), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(KeyChangedEvent(source.Id, "SECOND", Edited), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Single().Key.Value.Should().Be("THIRD");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var source = SourceProject.Any();
        _dbContext.AddProjects(new DemandProject(source, Created));

        // Act
        await _handler.Handle(new ProjectDeletedEvent(source.Id, EventActor.System, Rekeyed), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(new ProjectDeletedEvent(Guid.CreateVersion7(), EventActor.System, Rekeyed), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.SaveChangesCallCount.Should().Be(0);
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
