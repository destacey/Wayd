using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProductManagement.Application.ProductTeams.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTeams.Commands;

public sealed class SyncProductTeamsCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeProductManagementDbContext _dbContext = new();
    private readonly SyncProductTeamsCommandHandler _handler;

    public SyncProductTeamsCommandHandlerTests()
    {
        _handler = new SyncProductTeamsCommandHandler(_dbContext, Mock.Of<ILogger<SyncProductTeamsCommandHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new ProductTeamFaker().Generate();

        // Act
        var result = await _handler.Handle(new SyncProductTeamsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.ProductTeams.Should().ContainSingle(t => t.Id == source.Id)
            .Which.Watermarks.Should().Be(TeamReplicaWatermarks.At(Read));
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheTeam_DeletesTheCopy()
    {
        // Arrange
        var kept = new ProductTeamFaker().Generate();
        _dbContext.AddProductTeam(new ProductTeam(kept, Created));
        _dbContext.AddProductTeam(new ProductTeam(new ProductTeamFaker().Generate(), Created));

        // Act
        await _handler.Handle(new SyncProductTeamsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.ProductTeams.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange
        var existing = new ProductTeamFaker().Generate();
        _dbContext.AddProductTeam(new ProductTeam(existing, Created));
        var createdAfterTheRead = new ProductTeam(new ProductTeamFaker().Generate(), AfterTheRead);
        _dbContext.AddProductTeam(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncProductTeamsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.ProductTeams.Should().Contain(t => t.Id == createdAfterTheRead.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new ProductTeam(new ProductTeamFaker().WithId(id).WithIsActive(true).Generate(), Created);
        copy.ApplyActivation(false, AfterTheRead);
        _dbContext.AddProductTeam(copy);
        var readBeforeTheDeactivation = new ProductTeamFaker().WithId(id).WithName(copy.Name).WithCode(copy.Code).WithIsActive(true).Generate();

        // Act
        await _handler.Handle(new SyncProductTeamsCommand([readBeforeTheDeactivation], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.ProductTeams.Single(t => t.Id == id).IsActive.Should().BeFalse();
    }
}
