using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Events.Planning.PlanningIntervals;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Models;
using Wayd.Planning.Application.PlanningIntervals.EventHandlers;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves the handler's sprint query and its mappings work against SQL Server: the sprints are filtered on the
/// nullable dates of their owned range, and the team's existing mappings are found through their loaded sprint.
/// </summary>
/// <remarks>The fake context runs the query as LINQ to Objects, so it cannot show either.</remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class TeamSprintAutoMappingHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate PiStart = new(2026, 1, 5);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ScrumTeamAdded_MapsItsSprintsAndRecordsTheMappingAsAutomation()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var seed = _fixture.CreateContext();
        var team = await PlanningSeed.Team(seed, ct);
        var otherTeam = await PlanningSeed.Team(seed, ct);
        var sprint1 = await PlanningSeed.Sprint(seed, team.Id, ct, new IterationDateRange(PiStart, PiStart.PlusDays(13)));
        var sprint2 = await PlanningSeed.Sprint(seed, team.Id, ct, new IterationDateRange(PiStart.PlusDays(14), PiStart.PlusDays(27)));
        await PlanningSeed.Sprint(seed, team.Id, ct, new IterationDateRange(PiStart, null));
        await PlanningSeed.Sprint(seed, otherTeam.Id, ct, new IterationDateRange(PiStart, PiStart.PlusDays(13)));

        var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(PiStart, PiStart.PlusDays(27)), 2, "Iteration ", EventActor.System, now).Value;
        interval.ManageTeams([team.Id, otherTeam.Id], EventActor.System, now);
        seed.PlanningIntervals.Add(interval);
        await seed.SaveChangesAsync(ct);

        var addedBy = Employee.Create(new PersonName("Ada", null, "Lovelace"), "E" + Guid.NewGuid().ToString("N")[..11], now,
            new EmailAddress($"ada.{Guid.NewGuid():N}@acme.example"), null, null, null, null, true, null, EventActor.System, now);
        seed.Employees.Add(addedBy);
        await seed.SaveChangesAsync(ct);

        var employeeId = addedBy.Id;
        var added = new PlanningIntervalTeamsChangedEvent(interval.Id, interval.Key, [team.Id], [], [team.Id, otherTeam.Id],
            EventActor.User("user-1", employeeId), now);

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var handler = new TeamSprintAutoMappingHandler(context, ScrumSchedules(team.Id), Clock(now), NullLogger<TeamSprintAutoMappingHandler>.Instance);
            await handler.Handle(added, ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var iterationIds = await verify.PlanningIntervals.AsNoTracking()
            .Where(pi => pi.Id == interval.Id)
            .SelectMany(pi => pi.Iterations)
            .OrderBy(i => i.DateRange.Start)
            .Select(i => i.Id)
            .ToListAsync(ct);
        var mappings = await verify.PlanningIntervalIterationSprints.AsNoTracking()
            .Where(m => m.PlanningIntervalId == interval.Id)
            .Select(m => new { m.PlanningIntervalIterationId, m.SprintId })
            .ToListAsync(ct);
        mappings.Should().BeEquivalentTo([
            new { PlanningIntervalIterationId = iterationIds[0], SprintId = sprint1.Id },
            new { PlanningIntervalIterationId = iterationIds[1], SprintId = sprint2.Id },
        ], "the undated sprint and the other team's sprint are not this team's to map");

        var entry = await verify.ActivityLogs.AsNoTracking()
            .SingleAsync(a => a.AggregateId == interval.Id && a.EventType == nameof(PlanningIntervalSprintMappingsChangedEvent), ct);
        entry.ActorKind.Should().Be(EventActorKind.Automation);
        entry.UserId.Should().Be("user-1");
        entry.EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public async Task Handle_TeamWithAMappingAlready_KeepsIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var seed = _fixture.CreateContext();
        var team = await PlanningSeed.Team(seed, ct);
        var sprint1 = await PlanningSeed.Sprint(seed, team.Id, ct, new IterationDateRange(PiStart, PiStart.PlusDays(13)));
        await PlanningSeed.Sprint(seed, team.Id, ct, new IterationDateRange(PiStart.PlusDays(14), PiStart.PlusDays(27)));

        var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(PiStart, PiStart.PlusDays(27)), 2, "Iteration ", EventActor.System, now).Value;
        interval.ManageTeams([team.Id], EventActor.System, now);
        seed.PlanningIntervals.Add(interval);
        await seed.SaveChangesAsync(ct);

        var second = interval.Iterations.OrderBy(i => i.DateRange.Start).Last();
        interval.MapSprintToIteration(second.Id, sprint1, EventActor.System, now).IsSuccess.Should().BeTrue();
        await seed.SaveChangesAsync(ct);

        var added = new PlanningIntervalTeamsChangedEvent(interval.Id, interval.Key, [team.Id], [], [team.Id], EventActor.System, now);

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var handler = new TeamSprintAutoMappingHandler(context, ScrumSchedules(team.Id), Clock(now), NullLogger<TeamSprintAutoMappingHandler>.Instance);
            await handler.Handle(added, ct);
        }

        // Assert — sprint 1 stays in the second iteration; sprint 2 belongs there too, so it is left unmapped,
        // and nothing else belongs to the first.
        await using var verify = _fixture.CreateContext();
        var mappings = await verify.PlanningIntervalIterationSprints.AsNoTracking()
            .Where(m => m.PlanningIntervalId == interval.Id)
            .Select(m => new { m.PlanningIntervalIterationId, m.SprintId })
            .ToListAsync(ct);
        mappings.Should().BeEquivalentTo([new { PlanningIntervalIterationId = second.Id, SprintId = sprint1.Id }]);
    }

    [Fact]
    public async Task Handle_SprintMappedInADeletedInterval_LeavesItAndMapsTheRest()
    {
        // Arrange — a deleted interval keeps its mappings, and a sprint may be mapped in one interval only.
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var seed = _fixture.CreateContext();
        var team = await PlanningSeed.Team(seed, ct);
        var sprint1 = await PlanningSeed.Sprint(seed, team.Id, ct, new IterationDateRange(PiStart, PiStart.PlusDays(13)));
        var sprint2 = await PlanningSeed.Sprint(seed, team.Id, ct, new IterationDateRange(PiStart.PlusDays(14), PiStart.PlusDays(27)));

        var deleted = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(PiStart, PiStart.PlusDays(27)), 2, "Iteration ", EventActor.System, now).Value;
        deleted.ManageTeams([team.Id], EventActor.System, now);
        seed.PlanningIntervals.Add(deleted);
        await seed.SaveChangesAsync(ct);
        deleted.MapSprintToIteration(deleted.Iterations.OrderBy(i => i.DateRange.Start).First().Id, sprint1, EventActor.System, now).IsSuccess.Should().BeTrue();
        await seed.SaveChangesAsync(ct);
        seed.PlanningIntervals.Remove(deleted);
        await seed.SaveChangesAsync(ct);

        var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(PiStart, PiStart.PlusDays(27)), 2, "Iteration ", EventActor.System, now).Value;
        interval.ManageTeams([team.Id], EventActor.System, now);
        seed.PlanningIntervals.Add(interval);
        await seed.SaveChangesAsync(ct);

        var added = new PlanningIntervalTeamsChangedEvent(interval.Id, interval.Key, [team.Id], [], [team.Id], EventActor.System, now);

        // Act
        await using (var context = _fixture.CreateContext())
        {
            var handler = new TeamSprintAutoMappingHandler(context, ScrumSchedules(team.Id), Clock(now), NullLogger<TeamSprintAutoMappingHandler>.Instance);
            await handler.Handle(added, ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var second = interval.Iterations.OrderBy(i => i.DateRange.Start).Last();
        var mappings = await verify.PlanningIntervalIterationSprints.AsNoTracking()
            .Where(m => m.PlanningIntervalId == interval.Id)
            .Select(m => new { m.PlanningIntervalIterationId, m.SprintId })
            .ToListAsync(ct);
        mappings.Should().BeEquivalentTo([new { PlanningIntervalIterationId = second.Id, SprintId = sprint2.Id }]);
    }

    private static IDispatcher ScrumSchedules(Guid teamId)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamsScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<TeamSchedulePeriodDto>>
            {
                [teamId] = [new TeamSchedulePeriodDto(new LocalDate(2024, 1, 2), null, "UTC", 1, SizingMethod.StoryPoints) { Methodology = Methodology.Scrum }],
            });
        return dispatcher.Object;
    }

    private static IDateTimeProvider Clock(Instant now) => Mock.Of<IDateTimeProvider>(p => p.Now == now);
}
