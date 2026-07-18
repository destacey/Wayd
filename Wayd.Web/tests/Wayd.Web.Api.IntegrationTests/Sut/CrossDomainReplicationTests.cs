using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Planning.Application.Persistence;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wayd.Work.Application.Persistence;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Cross-domain replication must be inline and synchronous: after a command that raises a replication
/// event returns, the same-Id projections in the consuming domains must already exist. Domain events are
/// dispatched via <c>IMessageBus.InvokeAsync</c> (which runs the handler inline in the calling scope);
/// <c>PublishAsync</c> would instead enqueue to Wolverine's buffered local queue and run the handler
/// later on a background thread, breaking read-your-writes for these same-Id copies.
/// </summary>
[Trait("Category", "Docker")]
public sealed class CrossDomainReplicationTests(WaydSqlServerApiFactory factory)
    : IClassFixture<WaydSqlServerApiFactory>
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task CreateTeam_ReplicatesToWorkPlanningAndPpm_SynchronouslyBeforeCommandReturns()
    {
        // Arrange
        _ = _factory.CreateClient();
        var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();

        Guid teamId;
        using (var dispatchScope = _factory.Services.CreateScope())
        {
            var dispatcher = dispatchScope.ServiceProvider.GetRequiredService<IDispatcher>();

            // Act — TeamCreatedEvent is raised inside SaveChanges; its replication handlers create the
            // same-Id WorkTeam / PlanningTeam / PpmTeam rows.
            var result = await dispatcher.Send(
                new CreateTeamCommand("Replication Test Team", new TeamCode(code), null, new LocalDate(2026, 1, 15)),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
            teamId = result.Value.Id;
        }

        // Assert — the projections must already be present. If events were buffered/async, these reads
        // would race and (usually) find nothing.
        using var readScope = _factory.Services.CreateScope();
        var sp = readScope.ServiceProvider;

        var workTeamExists = await sp.GetRequiredService<IWorkDbContext>().WorkTeams
            .AnyAsync(t => t.Id == teamId, TestContext.Current.CancellationToken);
        var planningTeamExists = await sp.GetRequiredService<IPlanningDbContext>().PlanningTeams
            .AnyAsync(t => t.Id == teamId, TestContext.Current.CancellationToken);
        var ppmTeamExists = await sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmTeams
            .AnyAsync(t => t.Id == teamId, TestContext.Current.CancellationToken);

        Assert.True(workTeamExists, "WorkTeam projection should exist synchronously after CreateTeam returns");
        Assert.True(planningTeamExists, "PlanningTeam projection should exist synchronously after CreateTeam returns");
        Assert.True(ppmTeamExists, "PpmTeam projection should exist synchronously after CreateTeam returns");
    }
}
