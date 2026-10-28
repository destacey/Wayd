using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.DemandManagement.Application.DemandProjects.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.DemandProjects.Commands;

public sealed class SyncDemandProjectsCommandHandlerTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeDemandManagementDbContext _dbContext = new();
    private readonly SyncDemandProjectsCommandHandler _handler;

    public SyncDemandProjectsCommandHandlerTests()
    {
        _handler = new SyncDemandProjectsCommandHandler(_dbContext, Mock.Of<ILogger<SyncDemandProjectsCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = SourceProject.Any();

        // Act
        var result = await _handler.Handle(new SyncDemandProjectsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.DemandProjects.Should().ContainSingle(p => p.Id == source.Id)
            .Which.Watermarks.Should().Be(DemandProjectWatermarks.At(Read));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheProject_DeletesTheCopy()
    {
        // Arrange
        var kept = SourceProject.Any("KEPT");
        _dbContext.AddProjects(new DemandProject(kept, Created), new DemandProject(SourceProject.Any("GONE"), Created));

        // Act
        await _handler.Handle(new SyncDemandProjectsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange — created after the read was taken, so the read could not see it.
        _dbContext.AddProjects(new DemandProject(SourceProject.Any(), AfterTheRead));

        // Act
        await _handler.Handle(new SyncDemandProjectsCommand([], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var source = SourceProject.Any(name: "Atlas");
        var copy = new DemandProject(source, Created);
        copy.ApplyName("Borealis", AfterTheRead);
        _dbContext.AddProjects(copy);

        // Act
        await _handler.Handle(new SyncDemandProjectsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.DemandProjects.Single().Name.Should().Be("Borealis");
    }
}
