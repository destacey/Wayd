using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkTeams.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.Commands;

public sealed class SyncWorkTeamsCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly SyncWorkTeamsCommandHandler _handler;

    public SyncWorkTeamsCommandHandlerTests()
    {
        _handler = new SyncWorkTeamsCommandHandler(_workDbContext, Mock.Of<ILogger<SyncWorkTeamsCommandHandler>>());
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new WorkTeamFaker(TeamType.Team).Generate();

        // Act
        var result = await _handler.Handle(new SyncWorkTeamsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _workDbContext.WorkTeams.Should().ContainSingle(t => t.Id == source.Id)
            .Which.Watermarks.Should().Be(TeamReplicaWatermarks.At(Read));
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheTeam_DeletesTheCopy()
    {
        // Arrange
        var kept = new WorkTeamFaker(TeamType.Team).Generate();
        _workDbContext.AddWorkTeam(new WorkTeam(kept, Created));
        _workDbContext.AddWorkTeam(new WorkTeam(new WorkTeamFaker(TeamType.Team).Generate(), Created));

        // Act
        var result = await _handler.Handle(new SyncWorkTeamsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _workDbContext.WorkTeams.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange — the team was created after the read, and its event has already built the copy.
        var existing = new WorkTeamFaker(TeamType.Team).Generate();
        _workDbContext.AddWorkTeam(new WorkTeam(existing, Created));
        var createdAfterTheRead = new WorkTeam(new WorkTeamFaker(TeamType.Team).Generate(), AfterTheRead);
        _workDbContext.AddWorkTeam(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncWorkTeamsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Should().Contain(t => t.Id == createdAfterTheRead.Id);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Atlas").Generate(), Created);
        copy.ApplyDetails("Borealis", new TeamCode("BOR"), AfterTheRead);
        _workDbContext.AddWorkTeam(copy);
        var readBeforeTheRename = new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Atlas").WithIsActive(copy.IsActive).Generate();

        // Act
        await _handler.Handle(new SyncWorkTeamsCommand([readBeforeTheRename], Read), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Single(t => t.Id == id).Name.Should().Be("Borealis");
    }
}
