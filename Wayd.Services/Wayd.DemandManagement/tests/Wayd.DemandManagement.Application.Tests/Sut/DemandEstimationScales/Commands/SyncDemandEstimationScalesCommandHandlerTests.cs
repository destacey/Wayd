using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.DemandManagement.Application.DemandEstimationScales.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models.Estimation;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.DemandEstimationScales.Commands;

public sealed class SyncDemandEstimationScalesCommandHandlerTests
{
    private static readonly Instant Copied = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant ReadAt = Copied.Plus(Duration.FromHours(1));

    private readonly FakeDemandManagementDbContext _dbContext = new();

    private SyncDemandEstimationScalesCommandHandler CreateHandler() =>
        new(_dbContext, Mock.Of<ILogger<SyncDemandEstimationScalesCommandHandler>>());

    [Fact]
    public async Task Handle_CreatesACopyForAScaleThatHasNone()
    {
        // Arrange
        var version = DemandEstimationScaleFactory.Version(1);
        var source = DemandEstimationScaleFactory.Snapshot(Guid.NewGuid(), "Fibonacci", true, [version]);

        // Act
        var result = await CreateHandler().Handle(new SyncDemandEstimationScalesCommand([source], ReadAt), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var copy = _dbContext.DemandEstimationScales.Should().ContainSingle().Subject;
        copy.Id.Should().Be(source.Id);
        copy.CurrentVersion.Id.Should().Be(version.VersionId);
        copy.Watermarks.Should().Be(DemandEstimationScaleWatermarks.At(ReadAt));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ResyncsACopyThatMissedChanges()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(name: "Old", isActive: true, asOf: Copied);
        _dbContext.AddEstimationScales(copy);
        var missed = DemandEstimationScaleFactory.Version(2);
        var source = DemandEstimationScaleFactory.Snapshot(copy.Id, "New", false,
            [new EstimationScaleVersionValues(copy.CurrentVersion.Id, 1, DemandEstimationScaleFactory.TShirtSizes), missed]);

        // Act
        await CreateHandler().Handle(new SyncDemandEstimationScalesCommand([source], ReadAt), TestContext.Current.CancellationToken);

        // Assert
        copy.Name.Should().Be("New");
        copy.IsActive.Should().BeFalse();
        copy.CurrentVersion.Id.Should().Be(missed.VersionId);
    }

    [Fact]
    public async Task Handle_RemovesTheCopyOfADeletedScaleNothingUses()
    {
        // Arrange
        _dbContext.AddEstimationScales(DemandEstimationScaleFactory.Create(asOf: Copied));

        // Act
        await CreateHandler().Handle(new SyncDemandEstimationScalesCommand([], ReadAt), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_KeepsTheCopyOfADeletedScaleAnEstimateStillUses_Inactive()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(isActive: true, asOf: Copied);
        _dbContext.AddEstimationScales(copy);
        _dbContext.AddRequests(new IntakeRequestFaker().WithEstimate(copy, "M").Generate());

        // Act
        await CreateHandler().Handle(new SyncDemandEstimationScalesCommand([], ReadAt), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().ContainSingle();
        copy.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_KeepsACopyThatChangedAfterTheRead()
    {
        // Arrange — created after the source was read, so its absence from the read says nothing
        var copy = DemandEstimationScaleFactory.Create(asOf: ReadAt.Plus(Duration.FromMinutes(1)));
        _dbContext.AddEstimationScales(copy);

        // Act
        await CreateHandler().Handle(new SyncDemandEstimationScalesCommand([], ReadAt), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().ContainSingle();
        copy.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_KeepsACopyCreatedJustBeforeTheRead()
    {
        // Arrange — the creation was stamped before the read but committed after it, so the read missed the scale
        var copy = DemandEstimationScaleFactory.Create(asOf: ReadAt.Minus(Duration.FromSeconds(2)));
        _dbContext.AddEstimationScales(copy);

        // Act
        await CreateHandler().Handle(new SyncDemandEstimationScalesCommand([], ReadAt), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandEstimationScales.Should().ContainSingle();
        copy.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LeavesAKeptCopyThatIsAlreadyInactiveUntouched()
    {
        // Arrange
        var copy = DemandEstimationScaleFactory.Create(isActive: false, asOf: Copied);
        _dbContext.AddEstimationScales(copy);
        _dbContext.AddRequests(new IntakeRequestFaker().WithEstimate(copy, "M").Generate());
        var watermarks = copy.Watermarks;

        // Act
        await CreateHandler().Handle(new SyncDemandEstimationScalesCommand([], ReadAt), TestContext.Current.CancellationToken);

        // Assert
        copy.IsActive.Should().BeFalse();
        copy.Watermarks.Should().Be(watermarks);
    }
}
