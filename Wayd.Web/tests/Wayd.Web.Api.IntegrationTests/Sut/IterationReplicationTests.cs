using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Planning.Application.Persistence;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Work.Application.Persistence;
using Wayd.Work.Domain.Models;
using Wolverine;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// A Work sprint is copied into Planning by the durable <c>Iteration*</c> events. One sync pass can change
/// several parts of a sprint at once, raising an event for each, and every one must reach the copy through
/// the real host.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class IterationReplicationTests(WaydSqlServerApiFactory factory)
{
    private static readonly IterationDateRange Range =
        new(new LocalDate(2026, 1, 1), new LocalDate(2026, 1, 14));

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task UpdateIteration_ReplicatesEveryChangedPartToPlanning()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var iterationId = await CreateReplicatedIteration(ct);
        var moved = new IterationDateRange(Range.Start, new LocalDate(2026, 1, 21));

        // Act — a rename and a moved end date in one save.
        using (var scope = _factory.Services.CreateScope())
        {
            var work = scope.ServiceProvider.GetRequiredService<IWorkDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
            var iteration = await work.Iterations.SingleAsync(i => i.Id == iterationId, ct);
            var result = iteration.Update("Sprint 1 (extended)", IterationType.Sprint, moved, null, EventActor.System, now);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
            await work.SaveChangesAsync(ct);
        }

        // Assert
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IPlanningDbContext>().PlanningSprints.AnyAsync(s =>
                s.Id == iterationId
                && s.Name == "Sprint 1 (extended)"
                && s.DateRange.End == moved.End, ct),
            ct), "the Planning copy should take the new name and dates");
    }

    [Fact]
    public async Task SupersededIterationUpdatedEvent_StillInTheOutbox_IsAppliedToTheCopy()
    {
        // Arrange — an envelope written as the superseded type before the switch, delivered through the real
        // durable route.
        var ct = TestContext.Current.CancellationToken;
        var iterationId = await CreateReplicatedIteration(ct);
        var key = await KeyOf(iterationId, ct);

        // Act
        using (var publishScope = _factory.Services.CreateScope())
        {
            var now = publishScope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
#pragma warning disable CS0618 // the retired type is exactly what is under test
            var legacy = new IterationUpdatedEvent(iterationId, key, "Legacy Sprint", IterationType.Sprint, IterationState.Active,
                new IterationDateRangeV1(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0)), null, EventActor.System, now.Plus(Duration.FromMinutes(1)));
#pragma warning restore CS0618

            await publishScope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(legacy);
        }

        // Assert
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IPlanningDbContext>().PlanningSprints.AnyAsync(s => s.Id == iterationId && s.Name == "Legacy Sprint", ct),
            ct), "the Planning copy should apply the superseded event");
    }

    [Fact]
    public async Task SupersededIterationDateRangeChangedEvent_StillInTheOutbox_AppliesTheUtcDates()
    {
        // Arrange — an envelope written as the superseded type before the switch, its dates as midnight UTC.
        var ct = TestContext.Current.CancellationToken;
        var iterationId = await CreateReplicatedIteration(ct);
        var key = await KeyOf(iterationId, ct);

        // Act
        using (var publishScope = _factory.Services.CreateScope())
        {
            var now = publishScope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
#pragma warning disable CS0618 // the retired type is exactly what is under test
            var legacy = new IterationDateRangeChangedEvent(iterationId, key,
                new IterationDateRangeV1(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 14, 0, 0)),
                new IterationDateRangeV1(Instant.FromUtc(2026, 1, 1, 0, 0), Instant.FromUtc(2026, 1, 28, 0, 0)),
                EventActor.System, now.Plus(Duration.FromMinutes(1)));
#pragma warning restore CS0618

            await publishScope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(legacy);
        }

        // Assert
        var end = new LocalDate(2026, 1, 28);
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IPlanningDbContext>().PlanningSprints.AnyAsync(s => s.Id == iterationId && s.DateRange.End == end, ct),
            ct), "the Planning copy should take the UTC date of the superseded event's end");
    }

    private async Task<int> KeyOf(Guid iterationId, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkDbContext>().Iterations
            .Where(i => i.Id == iterationId).Select(i => i.Key).SingleAsync(ct);
    }

    private async Task<Guid> CreateReplicatedIteration(CancellationToken ct)
    {
        Guid iterationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var work = scope.ServiceProvider.GetRequiredService<IWorkDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
            var iteration = Iteration.Create("Sprint 1", IterationType.Sprint, Range, null,
                OwnershipInfo.CreateWaydOwned(), [], EventActor.System, now);
            await work.Iterations.AddAsync(iteration, ct);
            await work.SaveChangesAsync(ct);
            iterationId = iteration.Id;
        }

        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IPlanningDbContext>().PlanningSprints.AnyAsync(s => s.Id == iterationId, ct),
            ct), "the Planning copy should exist before the change under test is made");

        return iterationId;
    }

    private async Task<bool> WaitFor(Func<IServiceProvider, Task<bool>> condition, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            if (await condition(scope.ServiceProvider))
            {
                return true;
            }

            await Task.Delay(250, ct);
        }

        return false;
    }
}
