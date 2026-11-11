using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.DemandProjectDeliverables.EventHandlers;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.DemandProjectDeliverables.EventHandlers;

/// <summary>
/// <see cref="DemandProjectDeliverableSyncHandler"/> keeps Demand Management's copy of each deliverable correct
/// however the durable events arrive: late, twice, out of order, or after the deliverable was removed.
/// </summary>
public sealed class DemandProjectDeliverableSyncHandlerTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Started = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant Accepted = Created.Plus(Duration.FromMinutes(10));

    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly DemandProjectDeliverableSyncHandler _handler;
    private readonly SourceProject _project = SourceProject.Any();

    public DemandProjectDeliverableSyncHandlerTests()
    {
        _handler = new DemandProjectDeliverableSyncHandler(_dbContext, _dispatcher.Object, Mock.Of<ILogger<DemandProjectDeliverableSyncHandler>>());
    }

    [Fact]
    public async Task Handle_Added_WhenNoCopyExists_CreatesItFromTheSourceStampedWithTheEvent()
    {
        // Arrange
        _dbContext.AddProjects(new DemandProject(_project, Created));
        var source = SourceProjectDeliverable.Any(_project.Id, "Ledger");
        SourceReturns(source);

        // Act
        await _handler.Handle(AddedEvent(source, Started), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandProjectDeliverables.Should().ContainSingle(d => d.Id == source.Id).Subject;
        copy.ProjectId.Should().Be(_project.Id);
        copy.Name.Should().Be("Ledger");
        copy.Watermarks.Should().Be(DemandProjectDeliverableWatermarks.At(Started));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Added_WhenTheProjectHasNoCopyYet_CreatesTheProjectCopyToo()
    {
        // Arrange — the project's own create is still in flight.
        var source = SourceProjectDeliverable.Any(_project.Id);
        SourceReturns(source);
        ProjectSourceReturns(_project);

        // Act
        await _handler.Handle(AddedEvent(source, Started), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().ContainSingle(p => p.Id == _project.Id)
            .Which.Watermarks.Should().Be(DemandProjectWatermarks.At(Started));
        _dbContext.DemandProjectDeliverables.Should().ContainSingle(d => d.Id == source.Id);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Added_WhenTheProjectNoLongerExists_CreatesNothing()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id);
        SourceReturns(source);
        ProjectSourceReturns(null);

        // Act
        await _handler.Handle(AddedEvent(source, Started), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().BeEmpty();
        _dbContext.DemandProjectDeliverables.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Added_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id);
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));

        // Act
        await _handler.Handle(AddedEvent(source, Created), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _dispatcher.Verify(d => d.Send(It.IsAny<GetSimpleProjectDeliverableQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Added_WhenDeliveredAfterTheDeliverableWasRemoved_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(AddedEvent(SourceProjectDeliverable.Any(_project.Id), Created), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_WhenNewer_RenamesAndSaves()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id, "Ledger");
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source, "General Ledger", Started), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Single().Name.Should().Be("General Ledger");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_StatusChanged_WhenNewer_MovesTheCopy()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id);
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));

        // Act
        await _handler.Handle(StatusChangedEvent(source, StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Accepted), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandProjectDeliverables.Single();
        copy.StatusCategory.Should().Be(StatusCategory.Done);
        copy.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_StatusChanged_DeliveredOutOfOrder_KeepsTheNewerStatus()
    {
        // Arrange — started then accepted, delivered acceptance first.
        var source = SourceProjectDeliverable.Any(_project.Id);
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));
        await _handler.Handle(StatusChangedEvent(source, StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Accepted), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(StatusChangedEvent(source, StatusCategory.Active, ProjectDeliverableStatusAlias.InProgress, Started), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Single().StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_StatusChanged_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange — the source already holds the acceptance.
        _dbContext.AddProjects(new DemandProject(_project, Created));
        var source = SourceProjectDeliverable.Accepted(_project.Id);
        SourceReturns(source);

        // Act
        await _handler.Handle(StatusChangedEvent(source, StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, Accepted), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Should().ContainSingle(d => d.Id == source.Id)
            .Which.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }

    [Fact]
    public async Task Handle_WorkflowSwitched_ForADeliverable_MovesTheCopy()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id);
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));

        // Act
        await _handler.Handle(SwitchedEvent(ProjectDeliverableWorkflow.OwnerType, source.Id, StatusCategory.Done, (int)ProjectDeliverableStatusAlias.Accepted, Accepted), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandProjectDeliverables.Single();
        copy.StatusCategory.Should().Be(StatusCategory.Done);
        copy.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WorkflowSwitched_ForAnotherOwnerType_IsIgnored()
    {
        // Arrange — another owner type's switch carrying the same record id must not move the copy.
        var source = SourceProjectDeliverable.Any(_project.Id);
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));

        // Act
        await _handler.Handle(SwitchedEvent("demand.intake-request", source.Id, StatusCategory.Done, 4, Accepted), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Single().StatusAlias.Should().Be(ProjectDeliverableStatusAlias.NotStarted);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _dispatcher.Verify(d => d.Send(It.IsAny<GetSimpleProjectDeliverableQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Removed_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id);
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));

        // Act
        await _handler.Handle(RemovedEvent(source, Started), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Removed_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(RemovedEvent(SourceProjectDeliverable.Any(_project.Id), Started), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_StatusChanged_AfterTheDeliverableWasRemoved_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(StatusChangedEvent(SourceProjectDeliverable.Any(_project.Id), StatusCategory.Active, ProjectDeliverableStatusAlias.InProgress, Started), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private void SourceReturns(ISimpleProjectDeliverable? deliverable) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleProjectDeliverableQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deliverable);

    private void ProjectSourceReturns(ISimpleProjectWithLifecycle? project) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleProjectWithLifecycleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

    private static ProjectDeliverableAddedEvent AddedEvent(SourceProjectDeliverable d, Instant timestamp) =>
        new(d.ProjectId, new ProjectKey("ATLAS"), d.Id, d.Name, null, null, 1, null, null, Guid.CreateVersion7(), "Not Started",
            d.StatusCategory, d.StatusAlias, EventActor.System, timestamp);

    private static ProjectDeliverableDetailsUpdatedEvent DetailsUpdatedEvent(SourceProjectDeliverable d, string name, Instant timestamp) =>
        new(d.ProjectId, new ProjectKey("ATLAS"), d.Id,
            new ProjectDeliverableDetails(name, null, null, null, null),
            new ProjectDeliverableDetails(d.Name, null, null, null, null),
            EventActor.System, timestamp);

    private static ProjectDeliverableStatusChangedEvent StatusChangedEvent(
        SourceProjectDeliverable d, StatusCategory category, ProjectDeliverableStatusAlias alias, Instant timestamp) =>
        new(d.ProjectId, new ProjectKey("ATLAS"), d.Id,
            Guid.CreateVersion7(), "Before", d.StatusCategory, d.StatusAlias,
            Guid.CreateVersion7(), "After", category, alias,
            null, EventActor.System, timestamp);

    private static ProjectDeliverableRemovedEvent RemovedEvent(SourceProjectDeliverable d, Instant timestamp) =>
        new(d.ProjectId, new ProjectKey("ATLAS"), d.Id, d.Name, EventActor.System, timestamp);

    private static StatusWorkflowSwitchedEvent SwitchedEvent(string ownerType, Guid recordId, StatusCategory toCategory, int toAlias, Instant timestamp) =>
        new(Guid.CreateVersion7(), ownerType, recordId,
            Guid.CreateVersion7(), Guid.CreateVersion7(), StatusCategory.Proposed, (int)ProjectDeliverableStatusAlias.NotStarted,
            Guid.CreateVersion7(), Guid.CreateVersion7(), toCategory, toAlias,
            null, EventActor.System, timestamp);
}
