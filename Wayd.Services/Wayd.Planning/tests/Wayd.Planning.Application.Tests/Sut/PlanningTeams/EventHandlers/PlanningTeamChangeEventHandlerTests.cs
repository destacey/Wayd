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
using Wayd.Planning.Application.PlanningTeams.EventHandlers;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningTeams.EventHandlers;

/// <summary>
/// <see cref="PlanningTeamChangeEventHandler"/> keeps the Planning copy of each team correct however the
/// durable <c>Team*</c> events arrive: late, twice, out of order, or after the team was deleted.
/// </summary>
public sealed class PlanningTeamChangeEventHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Renamed = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant Reactivated = Created.Plus(Duration.FromMinutes(10));

    private readonly FakePlanningDbContext _planningDbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly PlanningTeamChangeEventHandler _handler;

    public PlanningTeamChangeEventHandlerTests()
    {
        _handler = new PlanningTeamChangeEventHandler(_planningDbContext, _dispatcher.Object, Mock.Of<ILogger<PlanningTeamChangeEventHandler>>());
    }

    public void Dispose() => _planningDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSource()
    {
        // Arrange
        var source = new PlanningTeamFaker(TeamType.Team).WithName("Atlas").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _planningDbContext.PlanningTeams.Should().ContainSingle(t => t.Id == source.Id).Subject;
        copy.Name.Should().Be("Atlas");
        copy.Watermarks.Should().Be(TeamReplicaWatermarks.At(Created));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var id = Guid.NewGuid();
        _planningDbContext.AddPlanningTeam(new PlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(CreatedEvent(id), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().ContainSingle(t => t.Id == id);
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheTeamWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Updated_WhenNewerThanTheCopy_AppliesAndSaves()
    {
        // Arrange
        var id = Guid.NewGuid();
        _planningDbContext.AddPlanningTeam(new PlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(new TeamUpdatedEvent(id, new TeamCode("BOR"), "Borealis", "desc", EventActor.System, Renamed), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Single(t => t.Id == id).Name.Should().Be("Borealis");
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange
        var source = new PlanningTeamFaker(TeamType.Team).WithName("Borealis").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(new TeamUpdatedEvent(source.Id, source.Code, "Borealis", "desc", EventActor.System, Renamed), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().ContainSingle(t => t.Id == source.Id).Which.Name.Should().Be("Borealis");
    }

    [Fact]
    public async Task Handle_ActivatedAndDeactivated_WhenDeliveredOutOfOrder_KeepTheLaterState()
    {
        // Arrange — deactivated at Renamed, reactivated at Reactivated; the reactivation arrives first.
        var id = Guid.NewGuid();
        _planningDbContext.AddPlanningTeam(new PlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).WithIsActive(true).Generate(), Created));

        // Act
        await _handler.Handle(new TeamActivatedEvent(id, EventActor.System, Reactivated), TestContext.Current.CancellationToken);
        await _handler.Handle(new TeamDeactivatedEvent(id, new LocalDate(2026, 12, 31), EventActor.System, Renamed), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Single(t => t.Id == id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Deleted_WhenTheCopyExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.NewGuid();
        _planningDbContext.AddPlanningTeam(new PlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(new TeamDeletedEvent(id, EventActor.System, Reactivated), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenRedelivered_IsNoOp()
    {
        // Arrange

        // Act
        await _handler.Handle(new TeamDeletedEvent(Guid.NewGuid(), EventActor.System, Reactivated), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
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
}
