using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.ProjectPortfolioManagement.Application.PpmTeams.EventHandlers;
using Wayd.ProjectPortfolioManagement.Application.Tests.Infrastructure;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Application.Tests.Sut.PpmTeams.EventHandlers;

/// <summary>
/// <see cref="PpmTeamChangeEventHandler"/> keeps the PPM copy of each team correct however the durable
/// <c>Team*</c> events arrive: late, twice, out of order, or after the team was deleted.
/// </summary>
public sealed class PpmTeamChangeEventHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Renamed = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant Reactivated = Created.Plus(Duration.FromMinutes(10));

    private readonly FakeProjectPortfolioManagementDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly PpmTeamChangeEventHandler _handler;

    public PpmTeamChangeEventHandlerTests()
    {
        _handler = new PpmTeamChangeEventHandler(_dbContext, _dispatcher.Object, Mock.Of<ILogger<PpmTeamChangeEventHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new PpmTeamFaker().WithName("Atlas").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _dbContext.PpmTeams.Should().ContainSingle(t => t.Id == source.Id).Subject;
        copy.Name.Should().Be("Atlas");
        copy.Watermarks.Should().Be(TeamReplicaWatermarks.At(Created));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheTeamWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Updated_WhenOlderThanTheCopy_IsSkipped()
    {
        // Arrange
        var id = Guid.NewGuid();
        _dbContext.AddPpmTeam(new PpmTeam(new PpmTeamFaker().WithId(id).WithName("Cassiopeia").Generate(), Reactivated));

        // Act
        await _handler.Handle(DetailsUpdatedEvent(id, new TeamCode("BOR"), "Borealis", Renamed), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Single(t => t.Id == id).Name.Should().Be("Cassiopeia");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SupersededUpdated_WhenNewerThanTheCopy_AppliesAndSaves()
    {
        // Arrange — an envelope written as the superseded type before the switch, still in the outbox.
        var id = Guid.NewGuid();
        _dbContext.AddPpmTeam(new PpmTeam(new PpmTeamFaker().WithId(id).WithName("Atlas").Generate(), Created));

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        await _handler.Handle(new TeamUpdatedEvent(id, new TeamCode("BOR"), "Borealis", "desc", EventActor.System, Renamed), TestContext.Current.CancellationToken);
#pragma warning restore CS0618

        // Assert
        _dbContext.PpmTeams.Single(t => t.Id == id).Name.Should().Be("Borealis");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange
        var source = new PpmTeamFaker().WithName("Borealis").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(DetailsUpdatedEvent(source.Id, source.Code, "Borealis", Renamed), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Should().ContainSingle(t => t.Id == source.Id).Which.Name.Should().Be("Borealis");
    }

    [Fact]
    public async Task Handle_ActivatedAndDeactivated_WhenDeliveredOutOfOrder_KeepTheLaterState()
    {
        // Arrange — deactivated at Renamed, reactivated at Reactivated; the reactivation arrives first.
        var id = Guid.NewGuid();
        _dbContext.AddPpmTeam(new PpmTeam(new PpmTeamFaker().WithId(id).WithIsActive(true).Generate(), Created));

        // Act
        await _handler.Handle(new TeamActivatedEvent(id, 1, new TeamCode("ABC01"), EventActor.System, Reactivated), TestContext.Current.CancellationToken);
        await _handler.Handle(new TeamDeactivatedEvent(id, 1, new TeamCode("ABC01"), new LocalDate(2026, 12, 31), EventActor.System, Renamed), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Single(t => t.Id == id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.NewGuid();
        _dbContext.AddPpmTeam(new PpmTeam(new PpmTeamFaker().WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(new TeamDeletedEvent(id, 1, new TeamCode("ABC01"), EventActor.System, Reactivated), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.PpmTeams.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(new TeamDeletedEvent(Guid.NewGuid(), 1, new TeamCode("ABC01"), EventActor.System, Reactivated), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    private void SourceReturns(ISimpleTeam? team) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleTeamQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

    private static TeamCreatedEvent CreatedEvent(Guid id) =>
        new(
            id: id,
            key: 1,
            code: new TeamCode("ABC01"),
            name: "Atlas",
            description: "desc",
            type: TeamType.Team,
            activeDate: new LocalDate(2026, 1, 1),
            inactiveDate: null,
            isActive: true,
            actor: EventActor.System,
            timestamp: Created);

    private static TeamDetailsUpdatedEvent DetailsUpdatedEvent(Guid id, TeamCode code, string name, Instant timestamp) =>
        new(id, 1, code, name, "desc", new TeamDetails(new TeamCode("ABC01"), "Atlas", "desc"), EventActor.System, timestamp);
}
