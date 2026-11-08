using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItemDependencies.Commands;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler finds dependencies through their source's workspace,
/// reads each end's sprint through navigations, and counts changes through the change tracker.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ProcessDependenciesCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "sys-1329";

    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_ReadsEachEndsSprintInItsTeamsZone()
    {
        // Arrange — both sprints end on Sunday Sep 27; 01:00 UTC on Sep 28 is still that evening in Chicago
        // and already the next morning in Tokyo, so only the predecessor is still planned: Healthy. Read in
        // UTC, both sprints would have ended: At Risk.
        await _seed.Reset(Ct);
        var chicagoTeam = await _seed.AddTeam(Ct);
        var tokyoTeam = await _seed.AddTeam(Ct);
        var sprintStart = new LocalDate(2026, 9, 14);
        var predecessor = await _seed.AddItem(chicagoTeam.Id, Ct, iterationId: await _seed.AddSprint(chicagoTeam.Id, sprintStart, Ct));
        var successor = await _seed.AddItem(tokyoTeam.Id, Ct, iterationId: await _seed.AddSprint(tokyoTeam.Id, sprintStart, Ct));
        var schedules = new TeamZoneSchedules((chicagoTeam.Id, "America/Chicago"), (tokyoTeam.Id, "Asia/Tokyo"));
        await _seed.LinkWorkspaceToSystem(SystemId, Guid.NewGuid(), Ct);
        await _seed.AddDependency(predecessor.Id, successor.Id, Ct);

        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var handler = new ProcessDependenciesCommandHandler(
                accessor.Context,
                schedules.Dispatcher.Object,
                schedules.SchedulingSettings.Object,
                schedules.GeneralSettings.Object,
                NullLogger<ProcessDependenciesCommandHandler>.Instance,
                Mock.Of<IDateTimeProvider>(p => p.Now == Instant.FromUtc(2026, 9, 28, 1, 0)));

            // Act
            var result = await handler.Handle(new ProcessDependenciesCommand(SystemId), Ct);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        await using var verify = _fixture.CreateContext();
        var dependency = await verify.WorkItemDependencies.SingleAsync(Ct);
        dependency.SourcePlannedOn.Should().Be(sprintStart.PlusDays(13));
        dependency.TargetPlannedOn.Should().BeNull();
        dependency.Health.Should().Be(DependencyPlanningHealth.Healthy);
    }

    [Fact]
    public async Task Handle_ClosesALinkOnceItsPredecessorIsDone_AndOpensItAgainWhenThePredecessorReopens()
    {
        // Arrange — neither end planned, so the open link is At Risk.
        await _seed.Reset(Ct);
        var (team, _) = await _seed.AddTeam(Ct);
        var predecessor = await _seed.AddItem(team, Ct, StatusCategory.Active);
        var successor = await _seed.AddItem(team, Ct);
        await _seed.LinkWorkspaceToSystem(SystemId, Guid.NewGuid(), Ct);
        await _seed.AddDependency(predecessor.Id, successor.Id, Ct);
        var schedules = new TeamZoneSchedules((team, "UTC"));

        // Act
        await SetStatusCategory(predecessor.Id, StatusCategory.Done);
        await Process(schedules);
        var closed = await StoredHealth();

        await SetStatusCategory(predecessor.Id, StatusCategory.Active);
        await Process(schedules);
        var reopened = await StoredHealth();

        // Assert
        closed.Should().Be(DependencyPlanningHealth.Closed);
        reopened.Should().Be(DependencyPlanningHealth.AtRisk);
    }

    private async Task Process(TeamZoneSchedules schedules)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new ProcessDependenciesCommandHandler(
            accessor.Context,
            schedules.Dispatcher.Object,
            schedules.SchedulingSettings.Object,
            schedules.GeneralSettings.Object,
            NullLogger<ProcessDependenciesCommandHandler>.Instance,
            Mock.Of<IDateTimeProvider>(p => p.Now == ForecastSeeder.Now));

        var result = await handler.Handle(new ProcessDependenciesCommand(SystemId), Ct);
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>Changes a seeded item's status the way a work item sync would, leaving its dependencies stale.</summary>
    private async Task SetStatusCategory(Guid workItemId, StatusCategory statusCategory)
    {
        await using var context = _fixture.CreateContext();
        var category = statusCategory.ToString();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Work].[WorkItems] SET [StatusCategory] = {category} WHERE [Id] = {workItemId}",
            Ct);
    }

    private async Task<DependencyPlanningHealth> StoredHealth()
    {
        await using var context = _fixture.CreateContext();
        return (await context.WorkItemDependencies.SingleAsync(Ct)).Health;
    }
}
