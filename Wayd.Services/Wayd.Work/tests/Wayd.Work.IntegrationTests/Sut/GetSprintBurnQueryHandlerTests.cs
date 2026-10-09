using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the handler reads history through the same work type tier lookup and
/// workspace navigation as sprint scope, which only a real provider translates.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetSprintBurnQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "sprint-burn-test";

    // A sprint with no team is counted in UTC with a one-day grace period and by item count: committed at the
    // end of Monday 14 September, ended at the end of Sunday 27 September.
    private static readonly LocalDate SprintStart = new(2026, 9, 14);
    private static readonly LocalDate SprintEnd = new(2026, 9, 27);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_SprintWithHistory_ReadsScopeAndCompletedEachDay()
    {
        // Arrange — two items committed, one done on day 6, and one added on day 3
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "BURN", ct);
        await WorkItemHistorySeeder.MarkHistoryReadToEnd(_fixture, workspaceId, ct);
        var sprintId = await WorkItemHistorySeeder.SeedSprint(_fixture, SprintStart, SprintEnd, ct);

        var done = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 1, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, done, workspaceId, 1, Day(-4), Day(6), sprintId, "Active", ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, done, workspaceId, 2, Day(6), null, sprintId, "Done", ct);

        var open = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 2, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, open, workspaceId, 1, Day(-4), null, sprintId, "Active", ct);

        var added = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 3, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, added, workspaceId, 1, Day(-4), Day(3), null, "Proposed", ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, added, workspaceId, 2, Day(3), null, sprintId, "Proposed", ct);

        // Act
        var result = await Handle(sprintId, ct);

        // Assert
        result.Should().NotBeNull();
        result!.HistoryIncomplete.Should().BeFalse();
        result.Committed.Count.Should().Be(2);
        result.Points.Should().HaveCount(14);
        Reading(result, SprintStart.PlusDays(1)).Should().Be((2, 0));
        Reading(result, SprintStart.PlusDays(3)).Should().Be((3, 0));
        Reading(result, SprintStart.PlusDays(6)).Should().Be((3, 1));
        Reading(result, SprintEnd).Should().Be((3, 1));
    }

    [Fact]
    public async Task Handle_IdealLine_IsFlatOnWeekendsHolidaysAndTeamDaysOff()
    {
        // Arrange — Monday to Friday by default, a holiday on Monday 21st and a team day off on Wednesday 23rd,
        // leaving seven working days: Tuesday 15th to Friday 18th, and the 22nd, 24th and 25th
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var sprintId = await WorkItemHistorySeeder.SeedSprint(_fixture, SprintStart, SprintEnd, ct);
        var holiday = new LocalDate(2026, 9, 21);
        var dayOff = new LocalDate(2026, 9, 23);
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var sprint = await accessor.Context.Iterations.SingleAsync(i => i.Id == sprintId, ct);
            sprint.SetTeamDaysOff([dayOff], EventActor.System, Instant.FromUtc(2026, 9, 1, 0, 0)).IsSuccess.Should().BeTrue();
            await accessor.Context.SaveChangesAsync(ct);
        }

        var dispatcher = HolidayDispatcher.With(holiday);

        // Act
        var result = await Handle(sprintId, ct, dispatcher.Object);

        // Assert
        result.Should().NotBeNull();
        Ideal(result!, new LocalDate(2026, 9, 19)).Should().BeApproximately(1 - 4.0 / 7, 1e-9);
        Ideal(result!, holiday).Should().Be(Ideal(result!, new LocalDate(2026, 9, 19)));
        Ideal(result!, holiday.PlusDays(1)).Should().Be(Ideal(result!, holiday));
        Ideal(result!, dayOff.PlusDays(1)).Should().Be(Ideal(result!, dayOff));
        Ideal(result!, new LocalDate(2026, 9, 26)).Should().Be(0);
        result!.Ideal[^1].Remaining.Should().Be(0);
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

    private static (int Scope, int Completed) Reading(SprintBurnDto burn, LocalDate day)
    {
        var point = burn.Points.Last(p => p.Day == day);
        return (point.Scope.Count, point.Completed.Count);
    }

    private static double Ideal(SprintBurnDto burn, LocalDate startOf) =>
        burn.Ideal.Single(p => p.At == startOf.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant()).Remaining;

    private async Task<SprintBurnDto?> Handle(Guid sprintId, CancellationToken ct, IDispatcher? dispatcher = null)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);

        var schedulingSettings = new Mock<ISettings<SchedulingSettings>>();
        schedulingSettings.Setup(s => s.Get(It.IsAny<CancellationToken>())).ReturnsAsync(new SchedulingSettings());

        // Read after the sprint has ended.
        var handler = new GetSprintBurnQueryHandler(accessor.Context, dispatcher ?? HolidayDispatcher.With().Object, schedulingSettings.Object,
            Mock.Of<IDateTimeProvider>(p => p.Now == Instant.FromUtc(2026, 10, 15, 12, 0)));
        return await handler.Handle(new GetSprintBurnQuery(new IdOrKey(sprintId.ToString())), ct);
    }
}
