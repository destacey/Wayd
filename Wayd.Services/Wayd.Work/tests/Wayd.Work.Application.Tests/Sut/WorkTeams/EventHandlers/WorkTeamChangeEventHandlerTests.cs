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
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkTeams.EventHandlers;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkTeams.EventHandlers;

/// <summary>
/// <see cref="WorkTeamChangeEventHandler"/> keeps the Work copy of each team correct however the durable
/// <c>Team*</c> events arrive: late, twice, out of order, or after the team was deleted.
/// </summary>
public sealed class WorkTeamChangeEventHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Renamed = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant Deactivated = Created.Plus(Duration.FromMinutes(10));

    private readonly FakeWorkDbContext _workDbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly WorkTeamChangeEventHandler _handler;

    public WorkTeamChangeEventHandlerTests()
    {
        _handler = new WorkTeamChangeEventHandler(_workDbContext, _dispatcher.Object, Mock.Of<ILogger<WorkTeamChangeEventHandler>>());
    }

    public void Dispose() => _workDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenNoCopyExists_CreatesItFromTheSourceStampedWithTheEvent()
    {
        // Arrange
        var source = new WorkTeamFaker(TeamType.Team).WithName("Atlas").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _workDbContext.WorkTeams.Should().ContainSingle(t => t.Id == source.Id).Subject;
        copy.Name.Should().Be("Atlas");
        copy.Watermarks.Should().Be(TeamReplicaWatermarks.At(Created));
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenRedelivered_IsNoOp()
    {
        // Arrange
        var id = Guid.NewGuid();
        _workDbContext.AddWorkTeam(new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).Generate(), Created));

        // Act
        await _handler.Handle(CreatedEvent(id), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Should().ContainSingle(t => t.Id == id);
        _workDbContext.SaveChangesCallCount.Should().Be(0);
        _dispatcher.Verify(d => d.Send(It.IsAny<GetSimpleTeamQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Created_WhenDeliveredAfterTheTeamWasDeleted_CreatesNothing()
    {
        // Arrange — the delete already ran; a redelivered create must not bring the copy back.
        SourceReturns(null);

        // Act
        await _handler.Handle(CreatedEvent(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Updated_WhenNewerThanTheCopy_AppliesAndSaves()
    {
        // Arrange
        var id = Guid.NewGuid();
        _workDbContext.AddWorkTeam(new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Atlas").Generate(), Created));

        // Act
        await _handler.Handle(UpdatedEvent(id, "Borealis", Renamed), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Single(t => t.Id == id).Name.Should().Be("Borealis");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenOlderThanTheCopy_IsSkipped()
    {
        // Arrange — two renames processed newest first.
        var id = Guid.NewGuid();
        _workDbContext.AddWorkTeam(new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).Generate(), Created));
        await _handler.Handle(UpdatedEvent(id, "Cassiopeia", Deactivated), TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(UpdatedEvent(id, "Borealis", Renamed), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Single(t => t.Id == id).Name.Should().Be("Cassiopeia");
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SupersededUpdated_WhenNewerThanTheCopy_AppliesAndSaves()
    {
        // Arrange — an envelope written as the superseded type before the switch, still in the outbox.
        var id = Guid.NewGuid();
        _workDbContext.AddWorkTeam(new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).WithName("Atlas").Generate(), Created));

        // Act
#pragma warning disable CS0618 // the retired type is exactly what is under test
        await _handler.Handle(new TeamUpdatedEvent(id, new TeamCode("BOR"), "Borealis", "desc", EventActor.System, Renamed), TestContext.Current.CancellationToken);
#pragma warning restore CS0618

        // Assert
        var copy = _workDbContext.WorkTeams.Single(t => t.Id == id);
        copy.Name.Should().Be("Borealis");
        copy.Code.Should().Be(new TeamCode("BOR"));
        _workDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenTheCreateHasNotArrived_CreatesTheCopyFromTheSource()
    {
        // Arrange — the rename is processed before the create, and the source already holds it.
        var source = new WorkTeamFaker(TeamType.Team).WithName("Borealis").Generate();
        SourceReturns(source);

        // Act
        await _handler.Handle(UpdatedEvent(source.Id, "Borealis", Renamed), TestContext.Current.CancellationToken);
        await _handler.Handle(CreatedEvent(source.Id), TestContext.Current.CancellationToken);

        // Assert
        var copy = _workDbContext.WorkTeams.Should().ContainSingle(t => t.Id == source.Id).Subject;
        copy.Name.Should().Be("Borealis");
        copy.Watermarks.Details.Should().Be(Renamed);
    }

    [Fact]
    public async Task Handle_Deactivated_WhenDeliveredAfterTheTeamWasDeleted_CreatesNothing()
    {
        // Arrange
        SourceReturns(null);

        // Act
        await _handler.Handle(new TeamDeactivatedEvent(Guid.NewGuid(), 1, new TeamCode("ABC01"), new LocalDate(2026, 1, 31), EventActor.System, Deactivated), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Should().BeEmpty();
        _workDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ActivatedAndDeactivated_WhenDeliveredOutOfOrder_KeepTheLaterState()
    {
        // Arrange — deactivated at Renamed, reactivated at Deactivated; the reactivation arrives first.
        var id = Guid.NewGuid();
        _workDbContext.AddWorkTeam(new WorkTeam(new WorkTeamFaker(TeamType.Team).WithId(id).WithIsActive(true).Generate(), Created));

        // Act
        await _handler.Handle(new TeamActivatedEvent(id, 1, new TeamCode("ABC01"), EventActor.System, Deactivated), TestContext.Current.CancellationToken);
        await _handler.Handle(new TeamDeactivatedEvent(id, 1, new TeamCode("ABC01"), new LocalDate(2026, 1, 31), EventActor.System, Renamed), TestContext.Current.CancellationToken);

        // Assert
        _workDbContext.WorkTeams.Single(t => t.Id == id).IsActive.Should().BeTrue();
    }

    private void SourceReturns(ISimpleTeam? team) =>
        _dispatcher
            .Setup(d => d.Send(It.IsAny<GetSimpleTeamQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

    private static TeamCreatedEvent CreatedEvent(Guid id) =>
        new(
            id: id,
            key: 1,
            code: new TeamCode("ATL"),
            name: "Atlas",
            description: "desc",
            type: TeamType.Team,
            activeDate: new LocalDate(2026, 1, 1),
            inactiveDate: null,
            isActive: true,
            actor: EventActor.System,
            timestamp: Created);

    private static TeamDetailsUpdatedEvent UpdatedEvent(Guid id, string name, Instant timestamp) =>
        new(id, 1, new TeamCode("ATL"), name, "desc", new TeamDetails(new TeamCode("ATL"), "Atlas", "desc"), EventActor.System, timestamp);
}
