using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models.SprintScope;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler reads history with a work type tier lookup inside the
/// projection and a workspace navigation in the completeness check, which only a real provider translates.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetSprintScopeQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "sprint-scope-test";

    // A sprint with no team is counted in UTC with a one-day grace period: committed at the end of
    // Monday 14 September, ended at the end of Sunday 27 September.
    private static readonly LocalDate SprintStart = new(2026, 9, 14);
    private static readonly LocalDate SprintEnd = new(2026, 9, 27);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_SprintWithHistory_ClassifiesEachItem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "SCOPE", ct);
        await WorkItemHistorySeeder.MarkHistoryReadToEnd(_fixture, workspaceId, ct);
        var sprintId = await WorkItemHistorySeeder.SeedSprint(_fixture, SprintStart, SprintEnd, ct);

        var committed = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 1, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, committed, workspaceId, 1, Day(-4), Day(6), sprintId, "Active", ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, committed, workspaceId, 2, Day(6), null, sprintId, "Done", ct);

        var added = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 2, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, added, workspaceId, 1, Day(-4), Day(3), null, "Proposed", ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, added, workspaceId, 2, Day(3), null, sprintId, "Active", ct);

        var descoped = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 3, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, descoped, workspaceId, 1, Day(-4), Day(4), sprintId, "Active", ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, descoped, workspaceId, 2, Day(4), null, null, "Active", ct);

        var neverIn = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 4, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, neverIn, workspaceId, 1, Day(-4), null, null, "Active", ct);

        // Act
        var result = await Handle(sprintId, ct);

        // Assert
        result.Should().NotBeNull();
        result!.HistoryIncomplete.Should().BeFalse();
        result.HasTeam.Should().BeFalse();
        result.SizingMethod.Should().Be(SizingMethod.Count);
        result.TimeZone.Should().Be("UTC");
        result.EffectiveStart.Should().Be(SprintStart.PlusDays(1).AtMidnight().InUtc().ToInstant());
        result.EffectiveEnd.Should().Be(SprintEnd.PlusDays(1).AtMidnight().InUtc().ToInstant());

        result.Items.Select(i => (i.WorkItem.Id, i.Entry, i.Outcome)).Should().BeEquivalentTo(new[]
        {
            (committed, SprintScopeEntry.Committed, SprintScopeOutcome.Completed),
            (added, SprintScopeEntry.Added, SprintScopeOutcome.CarriedOver),
            (descoped, SprintScopeEntry.Committed, SprintScopeOutcome.Descoped),
        });
        result.Totals.Committed.Count.Should().Be(2);
        result.Totals.SayDoCount.Should().Be(0.5);
    }

    [Fact]
    public async Task Handle_WorkspaceHistoryNotReadToTheEnd_IsHistoryIncomplete()
    {
        // Arrange — an item in the sprint now, in a workspace whose history has never been read
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "SCOPE", ct);
        var sprintId = await WorkItemHistorySeeder.SeedSprint(_fixture, SprintStart, SprintEnd, ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 1, ct);
        await PutInSprint(workItemId, sprintId, ct);

        // Act
        var result = await Handle(sprintId, ct);

        // Assert
        result.Should().NotBeNull();
        result!.HistoryIncomplete.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownSprint_ReturnsNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await Handle(Guid.NewGuid(), ct);

        // Assert
        result.Should().BeNull();
    }

    private static Instant Day(int offset) =>
        SprintStart.PlusDays(offset).At(new LocalTime(12, 0)).InUtc().ToInstant();

    private async Task<Application.WorkItems.Dtos.SprintScopeDto?> Handle(Guid sprintId, CancellationToken ct)
    {
        MapsterConfiguration.Ensure();
        await using var accessor = new WaydDbContextAccessor(_fixture);

        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        // Read after the sprint has ended.
        var handler = new GetSprintScopeQueryHandler(accessor.Context, HolidayDispatcher.With().Object, schedulingSettings.Object,
            Mock.Of<IDateTimeProvider>(p => p.Now == Instant.FromUtc(2026, 10, 15, 12, 0)));
        return await handler.Handle(new GetSprintScopeQuery(new IdOrKey(sprintId.ToString())), ct);
    }

    private async Task PutInSprint(Guid workItemId, Guid sprintId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Work].[WorkItems] SET [IterationId] = {sprintId} WHERE [Id] = {workItemId};", ct);
    }
}
