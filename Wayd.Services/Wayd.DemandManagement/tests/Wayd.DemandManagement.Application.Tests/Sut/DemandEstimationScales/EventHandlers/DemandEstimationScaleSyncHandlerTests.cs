using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Common.Domain.Models.Planning.EstimationScales;
using Wayd.DemandManagement.Application.DemandEstimationScales.EventHandlers;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models.Estimation;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.DemandEstimationScales.EventHandlers;

/// <summary>
/// <see cref="DemandEstimationScaleSyncHandler"/> keeps Demand Management's copy of each estimation scale correct
/// however the durable events arrive: late, twice, out of order, or after the scale was deleted.
/// </summary>
public sealed class DemandEstimationScaleSyncHandlerTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(5));

    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly DemandEstimationScaleSyncHandler _handler;

    public DemandEstimationScaleSyncHandlerTests()
    {
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetEstimationScaleSnapshotsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EstimationScaleSnapshot>());
        _handler = new DemandEstimationScaleSyncHandler(_dbContext, _dispatcher.Object, Mock.Of<ILogger<DemandEstimationScaleSyncHandler>>());
    }

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var version = DemandEstimationScaleFactory.Version(1);
        var source = DemandEstimationScaleFactory.Snapshot(Guid.NewGuid(), "T-Shirt Sizes", true, [version]);
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source, version), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandEstimationScales.Should().ContainSingle(s => s.Id == source.Id).Subject;
        copy.Name.Should().Be("T-Shirt Sizes");
        copy.IsActive.Should().BeTrue();
        copy.CurrentVersion.Id.Should().Be(version.VersionId);
        copy.Watermarks.Should().Be(DemandEstimationScaleWatermarks.At(Created));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(asOf: Created);
        _dbContext.AddEstimationScales(copy);
        var source = DemandEstimationScaleFactory.Snapshot(copy.Id, copy.Name, true, [DemandEstimationScaleFactory.Version(1)]);

        // Act
        await _handler.Handle(CreatedEvent(source, source.Versions[0]), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _dispatcher.Verify(d => d.Send(It.IsAny<GetEstimationScaleSnapshotsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Created_ForAScaleDeletedSince_CreatesNoCopy()
    {
        // Arrange
        var source = DemandEstimationScaleFactory.Snapshot(Guid.NewGuid(), "Gone", true, [DemandEstimationScaleFactory.Version(1)]);

        // Act
        await _handler.Handle(CreatedEvent(source, source.Versions[0]), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_RenamesTheCopy()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(name: "Old", asOf: Created);
        _dbContext.AddEstimationScales(copy);

        // Act
        await _handler.Handle(new EstimationScaleDetailsUpdatedEvent(copy.Id, copy.Key, "New", null,
            new EstimationScaleDetails("Old", null), EventActor.System, Later), TestContext.Current.CancellationToken);

        // Assert
        copy.Name.Should().Be("New");
        copy.Watermarks.Name.Should().Be(Later);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DetailsUpdated_OlderThanTheCopy_IsSkipped()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(name: "Current", asOf: Later);
        _dbContext.AddEstimationScales(copy);

        // Act
        await _handler.Handle(new EstimationScaleDetailsUpdatedEvent(copy.Id, copy.Key, "Stale", null,
            new EstimationScaleDetails("Older", null), EventActor.System, Created), TestContext.Current.CancellationToken);

        // Assert
        copy.Name.Should().Be("Current");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Deactivated_ThenActivated_FollowsTheLatest()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(isActive: true, asOf: Created);
        _dbContext.AddEstimationScales(copy);

        // Act
        await _handler.Handle(new EstimationScaleDeactivatedEvent(copy.Id, copy.Key, EventActor.System, Later), TestContext.Current.CancellationToken);
        var afterDeactivation = copy.IsActive;
        await _handler.Handle(new EstimationScaleActivatedEvent(copy.Id, copy.Key, EventActor.System, Later.Plus(Duration.FromMinutes(1))), TestContext.Current.CancellationToken);

        // Assert
        afterDeactivation.Should().BeFalse();
        copy.IsActive.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_VersionPublished_AddsTheVersion()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(asOf: Created);
        _dbContext.AddEstimationScales(copy);
        var previous = copy.CurrentVersion;
        var published = DemandEstimationScaleFactory.Version(2, [new("XS", true), new("S", true)]);

        // Act
        await _handler.Handle(new EstimationScaleVersionPublishedEvent(copy.Id, copy.Key, published, previous.Id, previous.Number,
            EventActor.System, Later), TestContext.Current.CancellationToken);

        // Assert
        copy.Versions.Should().HaveCount(2);
        copy.CurrentVersion.Id.Should().Be(published.VersionId);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_VersionPublished_ForAScaleWithNoCopy_CreatesItFromTheSource()
    {
        // Arrange
        var v1 = DemandEstimationScaleFactory.Version(1);
        var v2 = DemandEstimationScaleFactory.Version(2);
        var source = DemandEstimationScaleFactory.Snapshot(Guid.NewGuid(), "Fibonacci", true, [v1, v2]);
        SourceReturns(source);

        // Act
        await _handler.Handle(new EstimationScaleVersionPublishedEvent(source.Id, source.Key, v2, v1.VersionId, 1,
            EventActor.System, Later), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.DemandEstimationScales.Should().ContainSingle().Subject;
        copy.Versions.Select(v => v.Id).Should().BeEquivalentTo([v1.VersionId, v2.VersionId]);
        copy.Watermarks.Should().Be(DemandEstimationScaleWatermarks.At(Later));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_RemovesTheCopy()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(asOf: Created);
        _dbContext.AddEstimationScales(copy);

        // Act
        await _handler.Handle(new EstimationScaleDeletedEvent(copy.Id, copy.Key, copy.Name, EventActor.System, Later), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WithNoCopy_IsNoOp()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        await _handler.Handle(new EstimationScaleDeletedEvent(id, 3, "Gone", EventActor.System, Later), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private void SourceReturns(EstimationScaleSnapshot source) =>
        _dispatcher
            .Setup(d => d.Send(It.Is<GetEstimationScaleSnapshotsQuery>(q => q.Id == source.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync([source]);

    private static EstimationScaleCreatedEvent CreatedEvent(EstimationScaleSnapshot source, EstimationScaleVersionValues version) =>
        new(source.Id, source.Key, source.Name, null, source.IsActive, version, EventActor.System, Created);

    [Fact]
    public async Task Handle_Deleted_WhileADeskStillPinsIt_KeepsTheCopyInactive()
    {
        // Arrange — a desk pinned the scale after Planning's check but before this event arrived
        var copy = DemandEstimationScaleFactory.Create(isActive: true, asOf: Created);
        _dbContext.AddEstimationScales(copy);
        _dbContext.AddDesks(new IntakeDeskFaker().WithEstimationScaleVersion(copy.CurrentVersion).Generate());

        // Act
        await _handler.Handle(new EstimationScaleDeletedEvent(copy.Id, copy.Key, copy.Name, EventActor.System, Later), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().ContainSingle().Which.Should().BeSameAs(copy);
        copy.IsActive.Should().BeFalse();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Activated_OlderThanTheDeletionOfAnInUseCopy_IsSkipped()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(isActive: true, asOf: Created);
        _dbContext.AddEstimationScales(copy);
        _dbContext.AddDesks(new IntakeDeskFaker().WithEstimationScaleVersion(copy.CurrentVersion).Generate());
        await _handler.Handle(new EstimationScaleDeletedEvent(copy.Id, copy.Key, copy.Name, EventActor.System, Later), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(new EstimationScaleActivatedEvent(copy.Id, copy.Key, EventActor.System, Created.Plus(Duration.FromMinutes(1))), TestContext.Current.CancellationToken);

        // Assert
        copy.IsActive.Should().BeFalse();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
