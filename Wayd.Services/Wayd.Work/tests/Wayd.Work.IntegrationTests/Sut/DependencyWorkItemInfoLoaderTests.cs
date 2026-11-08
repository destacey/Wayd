using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItemDependencies;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the loader reads each item's sprint through a navigation in a
/// projection, which the fakes never translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DependencyWorkItemInfoLoaderTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;
    private readonly ForecastSeeder _seed = new(fixture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Load_ReadsEachSprintsLastDayInItsTeamsZone()
    {
        // Arrange — both sprints end on Sunday Sep 27; 01:00 UTC on Sep 28 is still that evening in Chicago
        // and already the next morning in Tokyo
        await _seed.Reset(Ct);
        var chicagoTeam = await _seed.AddTeam(Ct);
        var tokyoTeam = await _seed.AddTeam(Ct);
        var sprintStart = new LocalDate(2026, 9, 14);
        var lastDay = sprintStart.PlusDays(13);
        var chicagoSprint = await _seed.AddSprint(chicagoTeam.Id, sprintStart, Ct);
        var tokyoSprint = await _seed.AddSprint(tokyoTeam.Id, sprintStart, Ct);

        var inChicagoSprint = await _seed.AddItem(chicagoTeam.Id, Ct, iterationId: chicagoSprint);
        var inTokyoSprint = await _seed.AddItem(tokyoTeam.Id, Ct, iterationId: tokyoSprint);
        var inNoSprint = await _seed.AddItem(chicagoTeam.Id, Ct, statusCategory: StatusCategory.Active);

        var schedules = new TeamZoneSchedules((chicagoTeam.Id, "America/Chicago"), (tokyoTeam.Id, "Asia/Tokyo"));

        await using var accessor = new WaydDbContextAccessor(_fixture);

        // Act
        var infos = await accessor.Context.LoadDependencyWorkItemInfos(
            schedules.Dispatcher.Object,
            schedules.SchedulingSettings.Object,
            schedules.GeneralSettings.Object,
            [inChicagoSprint.Id, inTokyoSprint.Id, inNoSprint.Id],
            Instant.FromUtc(2026, 9, 28, 1, 0),
            Ct);

        // Assert
        infos.Should().HaveCount(3);
        infos[inChicagoSprint.Id].PlannedOn.Should().Be(lastDay);
        infos[inTokyoSprint.Id].PlannedOn.Should().BeNull();
        infos[inNoSprint.Id].PlannedOn.Should().BeNull();
        infos[inNoSprint.Id].StatusCategory.Should().Be(StatusCategory.Active);
    }
}
