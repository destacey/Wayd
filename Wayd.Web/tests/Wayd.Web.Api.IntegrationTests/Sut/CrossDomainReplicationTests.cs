using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Planning.Application.Persistence;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Work.Application.Persistence;
using Wolverine;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Cross-domain replication: an Organization <c>Team</c> is projected (same Id) into the Work, Planning, and
/// PPM domains via the <c>Team*</c> events. Those events are routed durably (see <c>DurableEventRoutes</c>),
/// so the projections are delivered on a background thread AFTER the command returns — not synchronously
/// in-request. These tests pin that creates and detail edits do arrive.
/// <para>
/// The safety of async Team replication rests on <c>ManagePlanningIntervalTeamsCommand</c> validating team
/// existence before inserting the required <c>PlanningIntervalTeam.TeamId</c> FK; that guard is covered by
/// <c>ManagePlanningIntervalTeamsCommandHandlerTests</c>.
/// </para>
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class CrossDomainReplicationTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task CreateTeam_ReplicatesToWorkPlanningAndPpm_AsynchronouslyAfterCommandReturns()
    {
        // Arrange
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        Guid teamId;
        int sourceKey;
        using (var dispatchScope = _factory.Services.CreateScope())
        {
            var dispatcher = dispatchScope.ServiceProvider.GetRequiredService<IDispatcher>();

            // Act — TeamCreatedEvent is enlisted in the outbox during SaveChanges and delivered post-commit;
            // its replication handlers create the same-Id WorkTeam / PlanningTeam / PpmTeam rows in the
            // background.
            var result = await dispatcher.Send(
                new CreateTeamCommand("Replication Test Team", new TeamCode(code), null, new LocalDate(2026, 1, 15)),
                ct);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
            teamId = result.Value.Id;

            // The source team's Key is database-generated (ValueGeneratedOnAdd). The event is raised in a
            // post-persistence action so it captures the assigned Key; capture it here to assert the
            // projections replicate the real value, not a default 0.
            sourceKey = await dispatchScope.ServiceProvider.GetRequiredService<IOrganizationDbContext>().Teams
                .AsNoTracking().Where(t => t.Id == teamId).Select(t => t.Key).SingleAsync(ct);
            Assert.NotEqual(0, sourceKey);
        }

        // Assert — the projections arrive eventually (background delivery). We assert arrival, not absence:
        // checking absence right after the command returns would be an inherent race with a quick agent. Each
        // projection must carry the real database-generated Key (regression guard: raising the post-persistence
        // event before the entity save would replicate Key = 0).
        var workTeamKey = await WaitForValue(
            sp => sp.GetRequiredService<IWorkDbContext>().WorkTeams.AsNoTracking().Where(t => t.Id == teamId).Select(t => (int?)t.Key).SingleOrDefaultAsync(ct),
            ct);
        var planningTeamKey = await WaitForValue(
            sp => sp.GetRequiredService<IPlanningDbContext>().PlanningTeams.AsNoTracking().Where(t => t.Id == teamId).Select(t => (int?)t.Key).SingleOrDefaultAsync(ct),
            ct);
        var ppmTeamKey = await WaitForValue(
            sp => sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmTeams.AsNoTracking().Where(t => t.Id == teamId).Select(t => (int?)t.Key).SingleOrDefaultAsync(ct),
            ct);

        Assert.True(workTeamKey.HasValue, "WorkTeam projection should be delivered asynchronously after CreateTeam returns");
        Assert.True(planningTeamKey.HasValue, "PlanningTeam projection should be delivered asynchronously after CreateTeam returns");
        Assert.True(ppmTeamKey.HasValue, "PpmTeam projection should be delivered asynchronously after CreateTeam returns");
        Assert.Equal(sourceKey, workTeamKey!.Value);
        Assert.Equal(sourceKey, planningTeamKey!.Value);
        Assert.Equal(sourceKey, ppmTeamKey!.Value);
    }

    [Fact]
    public async Task UpdateTeam_ReplicatesTheNewDetailsToWorkPlanningAndPpm()
    {
        // Arrange — a team whose three copies have all arrived.
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var teamId = await CreateReplicatedTeam(ct);
        var newCode = new TeamCode($"R{Guid.NewGuid():N}"[..8].ToUpperInvariant());

        // Act
        using (var dispatchScope = _factory.Services.CreateScope())
        {
            var result = await dispatchScope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
                new UpdateTeamCommand(teamId, $"Renamed Team {newCode.Value}", newCode, null),
                ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        }

        // Assert
        Assert.True(await WaitFor(
            async sp =>
            {
                var copy = await sp.GetRequiredService<IWorkDbContext>().WorkTeams.Where(t => t.Id == teamId).Select(t => new { t.Name, t.Code }).SingleAsync(ct);
                return copy.Name == $"Renamed Team {newCode.Value}" && copy.Code == newCode;
            },
            ct), "WorkTeam copy should take the new name and code");
        Assert.True(await WaitFor(
            async sp =>
            {
                var copy = await sp.GetRequiredService<IPlanningDbContext>().PlanningTeams.Where(t => t.Id == teamId).Select(t => new { t.Name, t.Code }).SingleAsync(ct);
                return copy.Name == $"Renamed Team {newCode.Value}" && copy.Code == newCode;
            },
            ct), "PlanningTeam copy should take the new name and code");
        Assert.True(await WaitFor(
            async sp =>
            {
                var copy = await sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmTeams.Where(t => t.Id == teamId).Select(t => new { t.Name, t.Code }).SingleAsync(ct);
                return copy.Name == $"Renamed Team {newCode.Value}" && copy.Code == newCode;
            },
            ct), "PpmTeam copy should take the new name and code");
    }

    [Fact]
    public async Task SupersededTeamUpdatedEvent_StillInTheOutbox_IsAppliedToEveryCopy()
    {
        // Arrange — an envelope written as the superseded type before the switch, delivered through the real
        // durable route, so a missing route or handler for it would leave the copies unrenamed.
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var teamId = await CreateReplicatedTeam(ct);
        var code = new TeamCode($"S{Guid.NewGuid():N}"[..8].ToUpperInvariant());

        // Act
        using (var publishScope = _factory.Services.CreateScope())
        {
            var now = publishScope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
#pragma warning disable CS0618 // the retired type is exactly what is under test
            var legacy = new TeamUpdatedEvent(teamId, code, $"Legacy Rename {code.Value}", null, EventActor.System, now.Plus(Duration.FromMinutes(1)));
#pragma warning restore CS0618

            await publishScope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(legacy);
        }

        // Assert
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IWorkDbContext>().WorkTeams.AnyAsync(t => t.Id == teamId && t.Name == $"Legacy Rename {code.Value}", ct),
            ct), "WorkTeam copy should apply the superseded event");
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IPlanningDbContext>().PlanningTeams.AnyAsync(t => t.Id == teamId && t.Name == $"Legacy Rename {code.Value}", ct),
            ct), "PlanningTeam copy should apply the superseded event");
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmTeams.AnyAsync(t => t.Id == teamId && t.Name == $"Legacy Rename {code.Value}", ct),
            ct), "PpmTeam copy should apply the superseded event");
    }

    private async Task<Guid> CreateReplicatedTeam(CancellationToken ct)
    {
        var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        Guid teamId;
        using (var dispatchScope = _factory.Services.CreateScope())
        {
            // Team names are unique, and every test in the collection shares the database.
            var result = await dispatchScope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
                new CreateTeamCommand($"Replication Team {code}", new TeamCode(code), null, new LocalDate(2026, 1, 15)),
                ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
            teamId = result.Value.Id;
        }

        Assert.True(await WaitFor(
            async sp => await sp.GetRequiredService<IWorkDbContext>().WorkTeams.AnyAsync(t => t.Id == teamId, ct)
                && await sp.GetRequiredService<IPlanningDbContext>().PlanningTeams.AnyAsync(t => t.Id == teamId, ct)
                && await sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmTeams.AnyAsync(t => t.Id == teamId, ct),
            ct), "every copy should exist before the change under test is made");

        return teamId;
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

    private async Task<T?> WaitForValue<T>(Func<IServiceProvider, Task<T?>> read, CancellationToken ct) where T : struct
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            var value = await read(scope.ServiceProvider);
            if (value.HasValue)
            {
                return value;
            }

            await Task.Delay(250, ct);
        }

        return null;
    }
}
