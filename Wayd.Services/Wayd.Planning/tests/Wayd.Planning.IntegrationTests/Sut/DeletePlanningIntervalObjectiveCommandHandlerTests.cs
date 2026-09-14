using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.Commands;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves an objective's deletion is recorded against the objective, by the save that soft-deletes it.
/// </summary>
/// <remarks>
/// The handler marks the objective deleted on the change tracker rather than through the interval's collection,
/// and the soft delete turns that into an update. Only a real save shows the event still drained from an entity
/// that save is removing.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeletePlanningIntervalObjectiveCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_RecordsTheDeletionAgainstTheObjective()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        Guid intervalId;
        Guid objectiveId;
        int objectiveKey;
        await using (var seed = _fixture.CreateContext())
        {
            var team = await PlanningSeed.Team(seed, ct);

            var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
                new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 1)), 4, "Iteration ", EventActor.System, now).Value;
            interval.ManageTeams([team.Id], EventActor.System, now);
            var objective = interval.CreateObjective(team, "Ship the thing", null, false, null, null, 1, EventActor.System, now).Value;
            seed.PlanningIntervals.Add(interval);
            await seed.SaveChangesAsync(ct);

            intervalId = interval.Id;
            objectiveId = objective.Id;
            objectiveKey = objective.Key;
        }

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            var handler = new DeletePlanningIntervalObjectiveCommandHandler(context, SqlServerDbContextFixture.CurrentUser(),
                SqlServerDbContextFixture.DateTimeProvider(now.Plus(Duration.FromHours(1))), NullLogger<DeletePlanningIntervalObjectiveCommandHandler>.Instance);
            result = await handler.Handle(new DeletePlanningIntervalObjectiveCommand(intervalId, objectiveId), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var verify = _fixture.CreateContext();
        (await verify.PlanningIntervalObjectives.IgnoreQueryFilters().AsNoTracking()
            .Where(o => o.Id == objectiveId)
            .Select(o => o.IsDeleted)
            .SingleAsync(ct)).Should().BeTrue();

        var entries = await verify.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == objectiveId)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .ToListAsync(ct);

        entries.Select(e => e.EventType).Should().Equal(
            nameof(PlanningIntervalObjectiveCreatedEvent),
            nameof(PlanningIntervalObjectiveDeletedEvent));
        entries.Should().OnlyContain(e => e.AggregateType == "PlanningIntervalObjective");
        entries.Should().OnlyContain(e => JsonDocument.Parse(e.Payload).RootElement.GetProperty("key").GetInt32() == objectiveKey);
    }
}
