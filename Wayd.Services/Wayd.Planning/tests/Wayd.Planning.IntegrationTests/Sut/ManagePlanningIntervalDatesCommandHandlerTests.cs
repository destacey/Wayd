using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Application.PlanningIntervals.Dtos;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves that removing an iteration takes the sprints mapped to it along, and records it.
/// </summary>
/// <remarks>
/// The handler has to load the interval's sprint mappings for the aggregate to see them. Against the fake context
/// <c>.Include</c> does nothing and the mappings are whatever the test put there, so only a real query shows the
/// removal reaching the table and the activity log.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class ManagePlanningIntervalDatesCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_IterationWithAMappedSprintRemoved_RemovesTheMappingAndRecordsIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        Guid intervalId;
        Guid removedIterationId;
        Guid sprintId;
        await using (var seed = _fixture.CreateContext())
        {
            var team = await PlanningSeed.Team(seed, ct);
            var sprint = await PlanningSeed.Sprint(seed, team.Id, ct);

            var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
                new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 1)), 4, "Iteration ", EventActor.System, now).Value;
            interval.ManageTeams([team.Id], EventActor.System, now);
            seed.PlanningIntervals.Add(interval);
            await seed.SaveChangesAsync(ct);

            removedIterationId = interval.Iterations.Last().Id;
            interval.MapSprintToIteration(removedIterationId, sprint, EventActor.System, now).IsSuccess.Should().BeTrue();
            await seed.SaveChangesAsync(ct);

            intervalId = interval.Id;
            sprintId = sprint.Id;
        }

        var command = await KeepAllIterationsBut(intervalId, removedIterationId, ct);

        // Act
        Result result;
        await using (var context = _fixture.CreateContext())
        {
            var handler = new ManagePlanningIntervalDatesCommandHandler(context, SqlServerDbContextFixture.CurrentUser(),
                SqlServerDbContextFixture.DateTimeProvider(), NullLogger<ManagePlanningIntervalDatesCommandHandler>.Instance);
            result = await handler.Handle(command, ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.PlanningIntervals.AsNoTracking()
            .Where(p => p.Id == intervalId)
            .SelectMany(p => p.IterationSprints)
            .AnyAsync(ct)).Should().BeFalse("the mapping goes with the iteration");

        var entries = await verify.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == intervalId)
            .ToListAsync(ct);

        var removed = entries.Should().ContainSingle(e => e.EventType == nameof(PlanningIntervalIterationRemovedEvent)).Subject;
        JsonDocument.Parse(removed.Payload).RootElement.GetProperty("iterationId").GetGuid().Should().Be(removedIterationId);

        var unmapped = entries
            .Where(e => e.EventType == nameof(PlanningIntervalSprintMappingsChangedEvent))
            .Select(e => JsonDocument.Parse(e.Payload).RootElement)
            .Should().ContainSingle(p => p.GetProperty("removed").GetArrayLength() > 0).Subject;
        unmapped.GetProperty("removed").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("sprintId").GetGuid().Should().Be(sprintId);
        unmapped.GetProperty("sprintMappings").GetArrayLength().Should().Be(0);
    }

    private async Task<ManagePlanningIntervalDatesCommand> KeepAllIterationsBut(Guid intervalId, Guid removedIterationId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var interval = await context.PlanningIntervals.AsNoTracking()
            .Include(p => p.Iterations)
            .SingleAsync(p => p.Id == intervalId, ct);

        return new ManagePlanningIntervalDatesCommand(
            interval.Id,
            interval.DateRange,
            [.. interval.Iterations
                .Where(i => i.Id != removedIterationId)
                .Select(i => new PlanningIntervalIterationUpsertDto(i.Id, i.Name, i.Category, i.DateRange))]);
    }
}
