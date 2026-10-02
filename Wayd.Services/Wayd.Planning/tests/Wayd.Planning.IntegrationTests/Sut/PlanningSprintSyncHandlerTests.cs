using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Application.PlanningSprints.EventHandlers;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves that a deleted sprint leaves the PI that mapped it, and that the PI records it.
/// </summary>
/// <remarks>
/// A mapping's foreign key refuses the copy's delete, so the handler has to find the mapping first, including
/// one held by a soft-deleted PI. The fake context neither applies the soft-delete filter nor enforces the key,
/// so only a real database shows either.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class PlanningSprintSyncHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Deleted = SqlServerDbContextFixture.FixedNow.Plus(Duration.FromMinutes(1));

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_IterationDeletedEventForAMappedSprint_UnmapsItAndRemovesTheCopy()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (intervalId, sprintId) = await SeedMappedSprint(softDeleteInterval: false, ct);

        // Act
        await HandleDeleted(sprintId, ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.PlanningSprints.AnyAsync(s => s.Id == sprintId, ct)).Should().BeFalse();
        (await verify.PlanningIntervalIterationSprints.AnyAsync(s => s.SprintId == sprintId, ct)).Should().BeFalse();

        var payloads = await verify.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == intervalId && a.EventType == nameof(PlanningIntervalSprintMappingsChangedEvent))
            .Select(a => a.Payload)
            .ToListAsync(ct);
        payloads.Select(p => JsonDocument.Parse(p).RootElement)
            .Should().ContainSingle(p => p.GetProperty("removed").GetArrayLength() > 0)
            .Which.GetProperty("removed").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("sprintId").GetGuid().Should().Be(sprintId);
    }

    [Fact]
    public async Task Handle_IterationDeletedEventForASprintMappedInADeletedInterval_UnmapsItAndRemovesTheCopy()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (_, sprintId) = await SeedMappedSprint(softDeleteInterval: true, ct);

        // Act
        await HandleDeleted(sprintId, ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.PlanningSprints.AnyAsync(s => s.Id == sprintId, ct)).Should().BeFalse();
        (await verify.PlanningIntervalIterationSprints.AnyAsync(s => s.SprintId == sprintId, ct)).Should().BeFalse();
    }

    private async Task HandleDeleted(Guid sprintId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var handler = new PlanningSprintSyncHandler(context, Mock.Of<IDispatcher>(), NullLogger<PlanningSprintSyncHandler>.Instance);
        await handler.Handle(new IterationDeletedEvent(sprintId, EventActor.System, Deleted), ct);
    }

    private async Task<(Guid IntervalId, Guid SprintId)> SeedMappedSprint(bool softDeleteInterval, CancellationToken ct)
    {
        var now = SqlServerDbContextFixture.FixedNow;

        await using var seed = _fixture.CreateContext();
        var team = await PlanningSeed.Team(seed, ct);
        var sprint = await PlanningSeed.Sprint(seed, team.Id, ct);

        var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 1)), 4, "Iteration ", EventActor.System, now).Value;
        interval.ManageTeams([team.Id], EventActor.System, now);
        seed.PlanningIntervals.Add(interval);
        await seed.SaveChangesAsync(ct);

        interval.MapSprintToIteration(interval.Iterations.First().Id, sprint, EventActor.System, now).IsSuccess.Should().BeTrue();
        await seed.SaveChangesAsync(ct);

        if (softDeleteInterval)
        {
            await SoftDelete(seed, interval, ct);
        }

        return (interval.Id, sprint.Id);
    }

    private static async Task SoftDelete(WaydDbContext seed, PlanningInterval interval, CancellationToken ct)
    {
        seed.PlanningIntervals.Remove(interval);
        await seed.SaveChangesAsync(ct);
        (await seed.PlanningIntervals.IgnoreQueryFilters().AnyAsync(p => p.Id == interval.Id && p.IsDeleted, ct))
            .Should().BeTrue("the context turns a removed interval into a soft delete");
    }
}
