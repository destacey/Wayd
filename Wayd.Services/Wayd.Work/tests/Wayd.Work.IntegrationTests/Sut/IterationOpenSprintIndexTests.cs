using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// The filtered unique index that keeps a team to one open sprint, against real SQL Server.
/// </summary>
/// <remarks>
/// Two starts that run at once each check the aggregate's rule against a timeline with no open sprint, so
/// both pass it; only the index stops the second save. The fakes have no index, so only a real provider
/// shows it holds.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class IterationOpenSprintIndexTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate SprintAStart = new(2026, 9, 14);
    private static readonly LocalDate SprintBStart = new(2026, 9, 28);

    // Sprint A has passed its default start, so it may be started late and sprint B early.
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 26, 12, 0);

    private static readonly SprintSchedule Utc = new(DateTimeZone.Utc, 1, SizingMethod.Count);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task ConcurrentStarts_OfTwoSprintsForOneTeam_SaveOnlyOne()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(cancellationToken);
        var teamId = await SeedTeam(cancellationToken);
        var sprintAId = await SeedSprint(teamId, "Sprint A", SprintAStart, cancellationToken);
        var sprintBId = await SeedSprint(teamId, "Sprint B", SprintBStart, cancellationToken);

        await using var first = new WaydDbContextAccessor(_fixture);
        await using var second = new WaydDbContextAccessor(_fixture);
        var firstTimeline = await Timeline(first, teamId, cancellationToken);
        var secondTimeline = await Timeline(second, teamId, cancellationToken);

        var startA = firstTimeline.Sprints.Single(s => s.Id == sprintAId)
            .Start(firstTimeline, Now, EventActor.System, Now);
        var startB = secondTimeline.Sprints.Single(s => s.Id == sprintBId)
            .Start(secondTimeline, Now, EventActor.System, Now);

        // Act
        await first.Context.SaveChangesAsync(cancellationToken);
        var secondSave = async () => await second.Context.SaveChangesAsync(cancellationToken);

        // Assert
        startA.IsSuccess.Should().BeTrue();
        startB.IsSuccess.Should().BeTrue();
        await secondSave.Should().ThrowAsync<DbUpdateException>();

        await using var verify = new WaydDbContextAccessor(_fixture);
        var open = await verify.Context.Iterations
            .Where(i => i.TeamId == teamId && i.Started != null && i.Completed == null)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);
        open.Should().ContainSingle().Which.Should().Be(sprintAId);
    }

    [Fact]
    public async Task OpenSprints_WhoseTeamsSyncUnmaps_StillSave()
    {
        // Arrange — two teams each with an open sprint
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(cancellationToken);
        var firstTeamId = await SeedTeam(cancellationToken);
        var secondTeamId = await SeedTeam(cancellationToken);
        var firstSprintId = await SeedSprint(firstTeamId, "Sprint A", SprintAStart, cancellationToken);
        var secondSprintId = await SeedSprint(secondTeamId, "Sprint B", SprintAStart, cancellationToken);

        await using (var start = new WaydDbContextAccessor(_fixture))
        {
            foreach (var teamId in new[] { firstTeamId, secondTeamId })
            {
                var timeline = await Timeline(start, teamId, cancellationToken);
                timeline.Sprints.Single().Start(timeline, Now, EventActor.System, Now).IsSuccess.Should().BeTrue();
            }
            await start.Context.SaveChangesAsync(cancellationToken);
        }

        await using var sync = new WaydDbContextAccessor(_fixture);
        var sprints = await sync.Context.Iterations
            .Where(i => i.Id == firstSprintId || i.Id == secondSprintId)
            .ToListAsync(cancellationToken);
        foreach (var sprint in sprints)
            sprint.Update(sprint.Name, sprint.Type, sprint.DateRange, teamId: null, EventActor.System, Now);

        // Act
        var save = async () => await sync.Context.SaveChangesAsync(cancellationToken);

        // Assert
        await save.Should().NotThrowAsync();
    }

    private static async Task<TeamSprintTimeline> Timeline(WaydDbContextAccessor accessor, Guid teamId, CancellationToken cancellationToken)
    {
        var sprints = await accessor.Context.Iterations
            .Where(i => i.TeamId == teamId)
            .ToListAsync(cancellationToken);

        return new TeamSprintTimeline(teamId, sprints, new TeamSprintSchedules([], Utc));
    }

    private async Task<Guid> SeedTeam(CancellationToken cancellationToken)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, "Atlas", new TeamCode($"T{key}"), TeamType.Team, true), Now);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkTeams.Add(team);
        await context.Context.SaveChangesAsync(cancellationToken);

        return team.Id;
    }

    private async Task<Guid> SeedSprint(Guid teamId, string name, LocalDate start, CancellationToken cancellationToken)
    {
        var sprint = Iteration.Create(name, IterationType.Sprint,
            new IterationDateRange(start, start.PlusDays(13)), teamId,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, Now);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.Iterations.Add(sprint);
        await context.Context.SaveChangesAsync(cancellationToken);

        return sprint.Id;
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
