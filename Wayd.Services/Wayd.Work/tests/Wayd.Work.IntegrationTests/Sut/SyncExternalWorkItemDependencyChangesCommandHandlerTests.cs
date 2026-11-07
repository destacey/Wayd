using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler matches links to work items through their workspace's
/// external id and reads each end's sprint through navigations.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SyncExternalWorkItemDependencyChangesCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handle_CreatingALink_ReadsEachEndsSprintInItsTeamsZone()
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

        var workspaceExternalId = Guid.NewGuid();
        await _seed.LinkWorkspaceToSystem("sys-1329", workspaceExternalId, Ct);
        var link = new ExternalLink
        {
            LinkType = WorkItemLinkType.Dependency,
            SourceId = ExternalIdOf(predecessor.Key),
            TargetId = ExternalIdOf(successor.Key),
            ChangedDate = Instant.FromUtc(2026, 9, 20, 9, 0),
            ChangedOperation = "create",
            IsActive = true,
            SourceWorkspaceId = workspaceExternalId,
            TargetWorkspaceId = workspaceExternalId,
        };

        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var handler = new SyncExternalWorkItemDependencyChangesCommandHandler(
                accessor.Context,
                schedules.Dispatcher.Object,
                schedules.SchedulingSettings.Object,
                schedules.GeneralSettings.Object,
                NullLogger<SyncExternalWorkItemDependencyChangesCommandHandler>.Instance,
                Mock.Of<IDateTimeProvider>(p => p.Now == Instant.FromUtc(2026, 9, 28, 1, 0)));

            // Act
            var result = await handler.Handle(new SyncExternalWorkItemDependencyChangesCommand(Guid.NewGuid(), [link]), Ct);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        await using var verify = _fixture.CreateContext();
        var dependency = await verify.WorkItemDependencies.SingleAsync(Ct);
        dependency.SourceId.Should().Be(predecessor.Id);
        dependency.TargetId.Should().Be(successor.Id);
        dependency.SourcePlannedOn.Should().Be(sprintStart.PlusDays(13));
        dependency.TargetPlannedOn.Should().BeNull();
        dependency.Health.Should().Be(DependencyPlanningHealth.Healthy);
    }

    // The seeder gives each item the number in its key as its external id.
    private static int ExternalIdOf(string key) => int.Parse(key[(key.LastIndexOf('-') + 1)..]);

    private sealed class ExternalLink : IExternalWorkItemLink
    {
        public WorkItemLinkType LinkType { get; set; }
        public int SourceId { get; set; }
        public int TargetId { get; set; }
        public Instant ChangedDate { get; set; }
        public string? ChangedBy { get; set; }
        public string? Comment { get; set; }
        public bool IsActive { get; set; }
        public string ChangedOperation { get; set; } = "";
        public Guid SourceWorkspaceId { get; set; }
        public Guid TargetWorkspaceId { get; set; }
    }
}
