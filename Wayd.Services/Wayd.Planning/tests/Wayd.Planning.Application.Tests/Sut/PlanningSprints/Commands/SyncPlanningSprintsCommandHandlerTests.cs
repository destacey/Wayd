using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningSprints.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PlanningSprints.Commands;

public sealed class SyncPlanningSprintsCommandHandlerTests : IDisposable
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Read = Created.Plus(Duration.FromMinutes(5));
    private static readonly Instant AfterTheRead = Read.Plus(Duration.FromMinutes(1));

    private readonly FakePlanningDbContext _planningDbContext = new();
    private readonly SyncPlanningSprintsCommandHandler _handler;

    public SyncPlanningSprintsCommandHandlerTests()
    {
        _handler = new SyncPlanningSprintsCommandHandler(_planningDbContext, Mock.Of<ILogger<SyncPlanningSprintsCommandHandler>>());
    }

    public void Dispose() => _planningDbContext.Dispose();

    [Fact]
    public async Task Handle_WhenTheSourceHasNoSprints_DeletesTheCopies()
    {
        // Arrange
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().Generate(), Created));
        var createdAfterTheRead = new PlanningSprint(new PlanningSprintFaker().Generate(), AfterTheRead);
        _planningDbContext.AddPlanningSprint(createdAfterTheRead);

        // Act
        var result = await _handler.Handle(new SyncPlanningSprintsCommand([], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _planningDbContext.PlanningSprints.Should().ContainSingle().Which.Id.Should().Be(createdAfterTheRead.Id);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenACopyIsMissing_CreatesItStampedWithTheRead()
    {
        // Arrange
        var source = new PlanningSprintFaker().Generate();

        // Act
        var result = await _handler.Handle(new SyncPlanningSprintsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _planningDbContext.PlanningSprints.Should().ContainSingle(s => s.Id == source.Id)
            .Which.Watermarks.Should().Be(PlanningSprintWatermarks.At(Read));
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenACopyDiffers_ResyncsIt()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new PlanningSprint(new PlanningSprintFaker().WithId(id).WithName("Sprint 1").Generate(), Created);
        _planningDbContext.AddPlanningSprint(copy);
        var source = SameSprintAs(copy).WithName("Sprint 1a").Generate();

        // Act
        var result = await _handler.Handle(new SyncPlanningSprintsCommand([source], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var synced = _planningDbContext.PlanningSprints.Should().ContainSingle().Subject;
        synced.Name.Should().Be("Sprint 1a");
        synced.Watermarks.Should().Be(PlanningSprintWatermarks.At(Created) with { Details = Read });
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheSourceNoLongerHasTheSprint_DeletesTheCopy()
    {
        // Arrange
        var kept = new PlanningSprintFaker().Generate();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(kept, Created));
        _planningDbContext.AddPlanningSprint(new PlanningSprint(new PlanningSprintFaker().Generate(), Created));

        // Act
        var result = await _handler.Handle(new SyncPlanningSprintsCommand([kept], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _planningDbContext.PlanningSprints.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadTookAChangeAfterIt_KeepsTheCopy()
    {
        // Arrange
        var existing = new PlanningSprintFaker().Generate();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(existing, Created));
        var createdAfterTheRead = new PlanningSprint(new PlanningSprintFaker().Generate(), AfterTheRead);
        _planningDbContext.AddPlanningSprint(createdAfterTheRead);

        // Act
        await _handler.Handle(new SyncPlanningSprintsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        _planningDbContext.PlanningSprints.Should().Contain(s => s.Id == createdAfterTheRead.Id);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenACopyMissingFromTheReadIsMappedByAPlanningInterval_KeepsTheCopyAndTheMapping()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31));
        var teamId = Guid.NewGuid();
        var planningInterval = new PlanningIntervalFaker().WithDateRange(piDates).WithTeams(teamId).WithIterations(piDates, 2, "Iteration ").Generate();
        var mapped = new PlanningSprint(new PlanningSprintFaker().AsSprint().WithTeamId(teamId).Generate(), Created);
        planningInterval.MapSprintToIteration(planningInterval.Iterations.First().Id, mapped, EventActor.System, Created).IsSuccess.Should().BeTrue();
        planningInterval.ClearDomainEvents();

        _planningDbContext.AddPlanningInterval(planningInterval);
        _planningDbContext.PlanningIntervalIterationSprints.Add(planningInterval.IterationSprints.Single());
        _planningDbContext.AddPlanningSprint(mapped);

        var existing = new PlanningSprintFaker().Generate();
        _planningDbContext.AddPlanningSprint(new PlanningSprint(existing, Created));
        var unmapped = new PlanningSprint(new PlanningSprintFaker().Generate(), Created);
        _planningDbContext.AddPlanningSprint(unmapped);

        // Act
        var result = await _handler.Handle(new SyncPlanningSprintsCommand([existing], Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _planningDbContext.PlanningSprints.Select(s => s.Id).Should().BeEquivalentTo([existing.Id, mapped.Id]);
        planningInterval.IterationSprints.Should().ContainSingle().Which.SprintId.Should().Be(mapped.Id);
        planningInterval.DomainEvents.Should().BeEmpty();
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenACopyHoldsAChangeNewerThanTheRead_DoesNotRollItBack()
    {
        // Arrange
        var id = Guid.NewGuid();
        var copy = new PlanningSprint(new PlanningSprintFaker().WithId(id).WithName("Sprint 1").Generate(), Created);
        copy.ApplyDetails("Sprint 2", copy.Type, AfterTheRead);
        _planningDbContext.AddPlanningSprint(copy);
        var readBeforeTheRename = SameSprintAs(copy).WithName("Sprint 1").Generate();

        // Act
        await _handler.Handle(new SyncPlanningSprintsCommand([readBeforeTheRename], Read), TestContext.Current.CancellationToken);

        // Assert
        var synced = _planningDbContext.PlanningSprints.Single(s => s.Id == id);
        synced.Name.Should().Be("Sprint 2");
        synced.Watermarks.Details.Should().Be(AfterTheRead);
    }

    [Fact]
    public async Task Handle_WhenAMappedSprintChangedTeam_UnmapsIt()
    {
        // Arrange
        var piDates = new LocalDateRange(new LocalDate(2026, 1, 1), new LocalDate(2026, 3, 31));
        var teamId = Guid.NewGuid();
        var planningInterval = new PlanningIntervalFaker().WithDateRange(piDates).WithTeams(teamId).WithIterations(piDates, 2, "Iteration ").Generate();
        var moved = new PlanningSprint(new PlanningSprintFaker().AsSprint().WithTeamId(teamId).Generate(), Created);
        var stayed = new PlanningSprint(new PlanningSprintFaker().AsSprint().WithTeamId(teamId).Generate(), Created);
        planningInterval.MapSprintToIteration(planningInterval.Iterations.First().Id, moved, EventActor.System, Created).IsSuccess.Should().BeTrue();
        planningInterval.MapSprintToIteration(planningInterval.Iterations.Last().Id, stayed, EventActor.System, Created).IsSuccess.Should().BeTrue();
        planningInterval.ClearDomainEvents();

        _planningDbContext.AddPlanningInterval(planningInterval);
        foreach (var mapping in planningInterval.IterationSprints)
            _planningDbContext.PlanningIntervalIterationSprints.Add(mapping);
        _planningDbContext.AddPlanningSprints([moved, stayed]);

        var otherTeamId = Guid.NewGuid();
        var sources = new[] { SameSprintAs(moved).WithTeamId(otherTeamId).Generate(), SameSprintAs(stayed).Generate() };

        // Act
        var result = await _handler.Handle(new SyncPlanningSprintsCommand(sources, Read), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        moved.TeamId.Should().Be(otherTeamId);
        planningInterval.IterationSprints.Should().ContainSingle().Which.SprintId.Should().Be(stayed.Id);
        planningInterval.DomainEvents.OfType<PlanningIntervalSprintMappingsChangedEvent>().Should().ContainSingle()
            .Which.Timestamp.Should().Be(Read);
        _planningDbContext.SaveChangesCallCount.Should().Be(1);
    }

    private static PlanningSprintFaker SameSprintAs(PlanningSprint sprint) =>
        new PlanningSprintFaker()
            .WithId(sprint.Id)
            .WithKey(sprint.Key)
            .WithName(sprint.Name)
            .WithType(sprint.Type)
            .WithDateRange(sprint.DateRange)
            .WithTeamId(sprint.TeamId);
}
