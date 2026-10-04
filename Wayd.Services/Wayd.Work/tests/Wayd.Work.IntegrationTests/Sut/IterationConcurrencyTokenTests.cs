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
/// The concurrency tokens on a sprint's actual dates, against real SQL Server.
/// </summary>
/// <remarks>
/// Two requests that start the same sprint at once each pass the aggregate's check, and the open-sprint index
/// cannot tell them apart because both write one row. Only the tokens stop the second from overwriting the
/// first's moment, and the fakes enforce no tokens.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class IterationConcurrencyTokenTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate SprintStart = new(2026, 9, 14);
    private static readonly Instant Now = Instant.FromUtc(2026, 9, 16, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task ConcurrentStarts_OfTheSameSprint_SaveOnlyTheFirst()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(cancellationToken);
        var teamId = await SeedTeam(cancellationToken);
        var sprintId = await SeedSprint(teamId, cancellationToken);
        var firstStart = Now.Minus(Duration.FromHours(2));
        var secondStart = Now.Minus(Duration.FromHours(1));

        await using var first = new WaydDbContextAccessor(_fixture);
        await using var second = new WaydDbContextAccessor(_fixture);
        var firstTimeline = await Timeline(first, teamId, cancellationToken);
        var secondTimeline = await Timeline(second, teamId, cancellationToken);

        firstTimeline.Sprints.Single().Start(firstTimeline, firstStart, EventActor.System, Now).IsSuccess.Should().BeTrue();
        secondTimeline.Sprints.Single().Start(secondTimeline, secondStart, EventActor.System, Now).IsSuccess.Should().BeTrue();

        // Act
        await first.Context.SaveChangesAsync(cancellationToken);
        var secondSave = async () => await second.Context.SaveChangesAsync(cancellationToken);

        // Assert
        await secondSave.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.Iterations.AsNoTracking().SingleAsync(i => i.Id == sprintId, cancellationToken);
        saved.Started.Should().Be(firstStart);
    }

    private static async Task<TeamSprintTimeline> Timeline(WaydDbContextAccessor accessor, Guid teamId, CancellationToken cancellationToken)
    {
        var sprints = await accessor.Context.Iterations
            .Where(i => i.TeamId == teamId)
            .ToListAsync(cancellationToken);

        return new TeamSprintTimeline(teamId, sprints, new TeamSprintSchedules([], new SprintSchedule(DateTimeZone.Utc, 1)));
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

    private async Task<Guid> SeedSprint(Guid teamId, CancellationToken cancellationToken)
    {
        var sprint = Iteration.Create("Sprint A", IterationType.Sprint,
            new IterationDateRange(SprintStart, SprintStart.PlusDays(13)), teamId,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, Now);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.Iterations.Add(sprint);
        await context.Context.SaveChangesAsync(cancellationToken);

        return sprint.Id;
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
