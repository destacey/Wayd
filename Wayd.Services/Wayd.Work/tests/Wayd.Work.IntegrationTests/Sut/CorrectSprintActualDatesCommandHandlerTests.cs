using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Correcting sprints' actual dates against real SQL Server.
/// </summary>
/// <remarks>
/// Correcting the team's open sprint closed and the next one open moves the open sprint. SQL Server checks the
/// open-sprint index after each statement, so the next sprint must not reach the database first. The fakes have
/// no index, so only a real provider shows it.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class CorrectSprintActualDatesCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDate Sprint1Start = new(2026, 9, 14);
    private static readonly LocalDate Sprint2Start = new(2026, 9, 28);
    private static readonly Instant Sprint1Started = Instant.FromUtc(2026, 9, 14, 15, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    // Seeded in both orders because updates reach the database in an order that follows the rows' keys.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_MovingTheOpenSprintToTheNext_SavesBoth(bool seedOpenSprintFirst)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(cancellationToken);
        var teamId = await SeedTeam(cancellationToken);
        Guid sprint1Id, sprint2Id;
        if (seedOpenSprintFirst)
        {
            sprint1Id = await SeedSprint(teamId, "Sprint 1", Sprint1Start, Sprint1Started, cancellationToken);
            sprint2Id = await SeedSprint(teamId, "Sprint 2", Sprint2Start, null, cancellationToken);
        }
        else
        {
            sprint2Id = await SeedSprint(teamId, "Sprint 2", Sprint2Start, null, cancellationToken);
            sprint1Id = await SeedSprint(teamId, "Sprint 1", Sprint1Start, Sprint1Started, cancellationToken);
        }
        var movedOn = Instant.FromUtc(2026, 9, 25, 20, 0);

        // Act — the sprint left open is listed first, so the handler's ordering is what keeps the index satisfied
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var result = await Handler(accessor, now: Instant.FromUtc(2026, 10, 2, 12, 0))
                .Handle(new CorrectSprintActualDatesCommand(
                [
                    new(sprint2Id, movedOn, null),
                    new(sprint1Id, Sprint1Started, movedOn),
                ]), cancellationToken);

            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var sprints = await verify.Context.Iterations.AsNoTracking()
            .Where(i => i.TeamId == teamId)
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        sprints[sprint1Id].Completed.Should().Be(movedOn);
        sprints[sprint2Id].Started.Should().Be(movedOn);
        sprints[sprint2Id].Completed.Should().BeNull();
    }

    private static CorrectSprintActualDatesCommandHandler Handler(WaydDbContextAccessor accessor, Instant now)
    {
        var dispatcher = new Mock<IDispatcher>();
        dispatcher
            .Setup(d => d.Send(It.IsAny<GetTeamScheduleHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-user");

        // System callers hold every permission, so this exercises the save rather than the membership check.
        var currentPrincipal = new Mock<ICurrentPrincipal>();
        currentPrincipal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new CorrectSprintActualDatesCommandHandler(
            accessor.Context,
            dispatcher.Object,
            schedulingSettings.Object,
            currentUser.Object,
            currentPrincipal.Object,
            Mock.Of<IDateTimeProvider>(p => p.Now == now),
            NullLogger<CorrectSprintActualDatesCommandHandler>.Instance);
    }

    private async Task<Guid> SeedTeam(CancellationToken cancellationToken)
    {
        var key = Random.Shared.Next(100_000, 999_999);
        var team = new WorkTeam(new SourceTeam(Guid.NewGuid(), key, "Atlas", new TeamCode($"T{key}"), TeamType.Team, true), SqlServerDbContextFixture.FixedNow);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.WorkTeams.Add(team);
        await context.Context.SaveChangesAsync(cancellationToken);

        return team.Id;
    }

    private async Task<Guid> SeedSprint(Guid teamId, string name, LocalDate start, Instant? started, CancellationToken cancellationToken)
    {
        var sprint = Iteration.Create(name, IterationType.Sprint,
            new IterationDateRange(start, start.PlusDays(13)), teamId,
            OwnershipInfo.CreateWaydOwned(), [], EventActor.System, SqlServerDbContextFixture.FixedNow);

        await using var context = new WaydDbContextAccessor(_fixture);
        context.Context.Iterations.Add(sprint);
        await context.Context.SaveChangesAsync(cancellationToken);

        if (started is { } at)
        {
            var timeline = new TeamSprintTimeline(teamId, [sprint], new TeamSprintSchedules([], new SprintSchedule(DateTimeZone.Utc, 1)));
            sprint.Start(timeline, at, EventActor.System, at).IsSuccess.Should().BeTrue();
            await context.Context.SaveChangesAsync(cancellationToken);
        }

        return sprint.Id;
    }

    private sealed record SourceTeam(Guid Id, int Key, string Name, TeamCode Code, TeamType Type, bool IsActive) : ISimpleTeam;
}
