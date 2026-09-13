using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProjectPortfolioManagement.Application.PpmTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.PpmTeams.Commands;

public sealed class SyncPpmTeamsCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly SyncPpmTeamsCommandHandler _handler;

    public SyncPpmTeamsCommandHandlerTests()
    {
        _handler = new SyncPpmTeamsCommandHandler(_dbContext, Mock.Of<ILogger<SyncPpmTeamsCommandHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new PpmTeamFaker().Generate();

        // Act
        var result = await _handler.Handle(new SyncPpmTeamsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.PpmTeams.Should().ContainSingle(t => t.Id == source.Id)
            .Which.Watermarks.Should().Be(TeamReplicaWatermarks.At(Read));
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheTeam_DeletesTheCopy()
    {
        // Arrange
        var kept = new PpmTeamFaker().Generate();
        _dbContext.AddPpmTeam(new PpmTeam(kept, Created));
        _dbContext.AddPpmTeam(new PpmTeam(new PpmTeamFaker().Generate(), Created));

        // Act
        await _handler.Handle(new SyncPpmTeamsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange
        var existing = new PpmTeamFaker().Generate();
        _dbContext.AddPpmTeam(new PpmTeam(existing, Created));
        var createdAfterTheRead = new PpmTeam(new PpmTeamFaker().Generate(), AfterTheRead);
        _dbContext.AddPpmTeam(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncPpmTeamsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Should().Contain(t => t.Id == createdAfterTheRead.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new PpmTeam(new PpmTeamFaker().WithId(id).WithIsActive(true).Generate(), Created);
        copy.ApplyActivation(false, AfterTheRead);
        _dbContext.AddPpmTeam(copy);
        var readBeforeTheDeactivation = new PpmTeamFaker().WithId(id).WithName(copy.Name).WithCode(copy.Code).WithIsActive(true).Generate();

        // Act
        await _handler.Handle(new SyncPpmTeamsCommand([readBeforeTheDeactivation], Read), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Single(t => t.Id == id).IsActive.Should().BeFalse();
    }
}
