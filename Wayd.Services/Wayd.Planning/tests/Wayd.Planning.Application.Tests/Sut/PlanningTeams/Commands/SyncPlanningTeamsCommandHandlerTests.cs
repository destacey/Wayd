using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Planning.Application.PlanningTeams.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningTeams.Commands;

public sealed class SyncPlanningTeamsCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakePlanningDbContext _planningDbContext = new();
    private readonly SyncPlanningTeamsCommandHandler _handler;

    public SyncPlanningTeamsCommandHandlerTests()
    {
        _handler = new SyncPlanningTeamsCommandHandler(_planningDbContext, Mock.Of<ILogger<SyncPlanningTeamsCommandHandler>>());
    }

    public void Dispose() => _planningDbContext.Dispose();

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new PlanningTeamFaker(TeamType.Team).Generate();

        // Act
        var result = await _handler.Handle(new SyncPlanningTeamsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _planningDbContext.PlanningTeams.Should().ContainSingle(t => t.Id == source.Id)
            .Which.Watermarks.Should().Be(TeamReplicaWatermarks.At(Read));
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheTeam_DeletesTheCopy()
    {
        // Arrange
        var kept = new PlanningTeamFaker(TeamType.Team).Generate();
        _planningDbContext.AddPlanningTeam(new PlanningTeam(kept, Created));
        _planningDbContext.AddPlanningTeam(new PlanningTeam(new PlanningTeamFaker(TeamType.Team).Generate(), Created));

        // Act
        await _handler.Handle(new SyncPlanningTeamsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange
        var existing = new PlanningTeamFaker(TeamType.Team).Generate();
        _planningDbContext.AddPlanningTeam(new PlanningTeam(existing, Created));
        var createdAfterTheRead = new PlanningTeam(new PlanningTeamFaker(TeamType.Team).Generate(), AfterTheRead);
        _planningDbContext.AddPlanningTeam(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncPlanningTeamsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().Contain(t => t.Id == createdAfterTheRead.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new PlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).WithIsActive(true).Generate(), Created);
        copy.ApplyActivation(false, AfterTheRead);
        _planningDbContext.AddPlanningTeam(copy);
        var readBeforeTheDeactivation = new PlanningTeamFaker(TeamType.Team).WithId(id).WithName(copy.Name).WithCode(copy.Code).WithIsActive(true).Generate();

        // Act
        await _handler.Handle(new SyncPlanningTeamsCommand([readBeforeTheDeactivation], Read), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Single(t => t.Id == id).IsActive.Should().BeFalse();
    }
}
