using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Persistence;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Planning.Application.Persistence;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Work.Application.Persistence;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Cross-domain replication: an Organization <c>Team</c> is projected (same Id) into the Work, Planning, and
/// PPM domains via the <c>Team*</c> events. Those events are routed durably (see <c>DurableEventRoutes</c>),
/// so the projections are delivered on a background thread AFTER <c>CreateTeamCommand</c> returns — not
/// synchronously in-request. This test pins that they do arrive.
/// <para>
/// The safety of async Team replication rests on <c>ManagePlanningIntervalTeamsCommand</c> validating team
/// existence before inserting the required <c>PlanningIntervalTeam.TeamId</c> FK; that guard is covered by
/// <c>ManagePlanningIntervalTeamsCommandHandlerTests</c>.
/// </para>
/// </summary>
[Trait("Category", "Docker")]
public sealed class CrossDomainReplicationTests(WaydSqlServerApiFactory factory)
    : IClassFixture<WaydSqlServerApiFactory>
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
