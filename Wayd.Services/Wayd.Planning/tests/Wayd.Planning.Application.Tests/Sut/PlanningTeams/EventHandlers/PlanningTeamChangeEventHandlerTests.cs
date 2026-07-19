using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Planning.Application.PlanningTeams.EventHandlers;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningTeams.EventHandlers;

/// <summary>
/// <see cref="PlanningTeamChangeEventHandler"/> replicates Organization Team changes into the Planning
/// <c>PlanningTeam</c> projection. Because it is delivered durably, its contract is: idempotent guards
/// short-circuit a redelivery, and any real failure PROPAGATES (Wolverine's retry/dead-letter governs it)
/// rather than being swallowed.
/// </summary>
public sealed class PlanningTeamChangeEventHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private readonly FakePlanningDbContext _planningDbContext = new();
    private readonly PlanningTeamChangeEventHandler _handler;

    public PlanningTeamChangeEventHandlerTests()
    {
        _handler = new PlanningTeamChangeEventHandler(_planningDbContext, Mock.Of<ILogger<PlanningTeamChangeEventHandler>>());
    }

    public void Dispose() => _planningDbContext.Dispose();

    [Fact]
    public async Task Handle_Created_WhenTeamIsNew_AddsProjectionAndSaves()
    {
        // Arrange
        var @event = CreatedEvent(Guid.CreateVersion7(), "Alpha");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().ContainSingle(t => t.Id == @event.Id);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Created_WhenTeamAlreadyExists_IsIdempotentNoOp()
    {
        // Arrange — a redelivery finds the projection already there.
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).Generate());

        var @event = CreatedEvent(id, "Duplicate");

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().ContainSingle(t => t.Id == id);
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Updated_WhenTeamExists_UpdatesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).Generate());

        var @event = new TeamUpdatedEvent(id, new TeamCode("NEW01"), "New Name", "desc", Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Single(t => t.Id == id).Name.Should().Be("New Name");
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Updated_WhenTeamMissing_IsNoOp()
    {
        // Arrange — out-of-order delivery: the update arrives before the create was applied.
        var @event = new TeamUpdatedEvent(Guid.CreateVersion7(), new TeamCode("NEW02"), "Whatever", "desc", Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Deactivated_WhenTeamExists_UpdatesActiveFlagAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).Generate());

        var @event = new TeamDeactivatedEvent(id, new LocalDate(2026, 12, 31), Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Single(t => t.Id == id).IsActive.Should().BeFalse();
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTeamExists_RemovesAndSaves()
    {
        // Arrange
        var id = Guid.CreateVersion7();
        _planningDbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(id).Generate());

        var @event = new TeamDeletedEvent(id, Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningTeams.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Deleted_WhenTeamMissing_IsIdempotentNoOp()
    {
        // Arrange — a redelivery of a delete that already ran.
        var @event = new TeamDeletedEvent(Guid.CreateVersion7(), Now);

        // Act
        await _handler.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.SaveChangesCallCount.Should().Be(0);
    }

    private static TeamCreatedEvent CreatedEvent(Guid id, string name) =>
        new(
            id: id,
            key: 1,
            code: new TeamCode("ABC01"),
            name: name,
            description: "desc",
            type: TeamType.Team,
            activeDate: new LocalDate(2026, 1, 1),
            inactiveDate: null,
            isActive: true,
            timestamp: Now);
}
