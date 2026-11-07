using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.DemandProjectDeliverables.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.DemandProjectDeliverables.Commands;

public sealed class SyncDemandProjectDeliverablesCommandHandlerTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly SyncDemandProjectDeliverablesCommandHandler _handler;
    private readonly SourceProject _project = SourceProject.Any();

    public SyncDemandProjectDeliverablesCommandHandlerTests()
    {
        _handler = new SyncDemandProjectDeliverablesCommandHandler(_dbContext, Mock.Of<ILogger<SyncDemandProjectDeliverablesCommandHandler>>());
        _dbContext.AddProjects(new DemandProject(_project, Created));
    }

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id);

        // Act
        var result = await _handler.Handle(new SyncDemandProjectDeliverablesCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.DemandProjectDeliverables.Should().ContainSingle(d => d.Id == source.Id)
            .Which.Watermarks.Should().Be(DemandProjectDeliverableWatermarks.At(Read));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheProjectHasNoCopy_SkipsTheDeliverable()
    {
        // Arrange
        var orphan = SourceProjectDeliverable.Any(Guid.CreateVersion7());

        // Act
        var result = await _handler.Handle(new SyncDemandProjectDeliverablesCommand([orphan], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.DemandProjectDeliverables.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheDeliverable_DeletesTheCopy()
    {
        // Arrange
        var kept = SourceProjectDeliverable.Any(_project.Id, "Kept");
        _dbContext.AddDeliverables(
            new DemandProjectDeliverable(kept, Created),
            new DemandProjectDeliverable(SourceProjectDeliverable.Any(_project.Id, "Gone"), Created));

        // Act
        await _handler.Handle(new SyncDemandProjectDeliverablesCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange — added after the read was taken, so the read could not see it.
        _dbContext.AddDeliverables(new DemandProjectDeliverable(SourceProjectDeliverable.Any(_project.Id), AfterTheRead));

        // Act
        await _handler.Handle(new SyncDemandProjectDeliverablesCommand([], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenTheStatusDrifted_OverwritesIt()
    {
        // Arrange — the copy missed the acceptance.
        var source = SourceProjectDeliverable.Any(_project.Id);
        _dbContext.AddDeliverables(new DemandProjectDeliverable(source, Created));

        // Act
        await _handler.Handle(new SyncDemandProjectDeliverablesCommand(
            [source with { StatusCategory = StatusCategory.Done, StatusAlias = ProjectDeliverableStatusAlias.Accepted }], Read),
            TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandProjectDeliverables.Single();
        copy.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        copy.Watermarks.Should().Be(new DemandProjectDeliverableWatermarks(Created, Read));
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var source = SourceProjectDeliverable.Any(_project.Id);
        var copy = new DemandProjectDeliverable(source, Created);
        copy.ApplyStatus(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, AfterTheRead);
        _dbContext.AddDeliverables(copy);

        // Act
        await _handler.Handle(new SyncDemandProjectDeliverablesCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjectDeliverables.Single().StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }
}
