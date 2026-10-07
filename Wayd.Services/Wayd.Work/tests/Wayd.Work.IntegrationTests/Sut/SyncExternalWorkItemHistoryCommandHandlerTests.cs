using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because what is under test is what the database holds: the periods
/// a batch leaves behind, the unique indexes that keep them contiguous, and the cascade that
/// deletes them with their work item.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SyncExternalWorkItemHistoryCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "history-sync-test";
    private const int ExternalWorkItemId = 501;
    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly Instant Start = Instant.FromUtc(2026, 3, 2, 9, 0);
    private static readonly Instant SyncedAt = Instant.FromUtc(2026, 4, 1, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_WithRevisions_StoresOnePeriodPerTrackedChangeAndTheWatermark()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);

        // Act
        var opened = await Sync(workspaceId, Revisions(), "token-1", ct);

        // Assert
        opened.Should().Be(3);
        await using var verify = new WaydDbContextAccessor(_fixture);
        var periods = await verify.Context.WorkItemStateHistory.AsNoTracking()
            .Where(h => h.WorkItemId == workItemId)
            .OrderBy(h => h.ValidFrom)
            .ToListAsync(ct);

        periods.Select(p => (p.Revision, p.StatusName, p.ValidFrom, p.ValidTo)).Should().Equal(
            (1, "New", Start, Start.Plus(Duration.FromDays(1))),
            (2, "Active", Start.Plus(Duration.FromDays(1)), Start.Plus(Duration.FromDays(3))),
            (4, "Closed", Start.Plus(Duration.FromDays(3)), (Instant?)null));
        periods.Should().OnlyContain(p => p.WorkspaceId == workspaceId && p.WorkTypeName == "History Story");

        (await WatermarkOf(verify.Context, workspaceId, ct)).Should().Be("token-1");
        (await BackfilledOnOf(verify.Context, workspaceId, ct)).Should().BeNull();
    }

    [Fact]
    public async Task Handle_TheLastBatch_MarksHistoryReadToEndOnlyTheFirstTime()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, _) = await Seed(ct);
        await Sync(workspaceId, Revisions().Take(2).ToList(), "token-1", ct);

        // Act
        await Sync(workspaceId, Revisions().Skip(2).ToList(), "token-2", ct, isLastBatch: true);
        await Sync(workspaceId, [], "token-2", ct, isLastBatch: true, now: SyncedAt.Plus(Duration.FromDays(1)));

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await BackfilledOnOf(verify.Context, workspaceId, ct)).Should().Be(SyncedAt);
        (await WatermarkOf(verify.Context, workspaceId, ct)).Should().Be("token-2");
    }

    [Fact]
    public async Task Handle_TheSameBatchTwice_ChangesNothing()
    {
        // Arrange — the watermark failed to save, so the source hands the batch over again
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, _) = await Seed(ct);
        await Sync(workspaceId, Revisions(), "token-1", ct);
        var before = await Snapshot(ct);

        // Act
        var opened = await Sync(workspaceId, Revisions(), "token-1", ct);

        // Assert
        opened.Should().Be(0);
        (await Snapshot(ct)).Should().Equal(before);
    }

    [Fact]
    public async Task Handle_ABatchContinuingTheLast_ClosesTheOpenPeriod()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, _) = await Seed(ct);
        await Sync(workspaceId, Revisions().Take(2).ToList(), "token-1", ct);

        // Act
        await Sync(workspaceId, Revisions().Skip(2).ToList(), "token-2", ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var periods = await verify.Context.WorkItemStateHistory.AsNoTracking().OrderBy(h => h.ValidFrom).ToListAsync(ct);
        periods.Select(p => p.StatusName).Should().Equal("New", "Active", "Closed");
        periods.Count(p => p.ValidTo == null).Should().Be(1);
        (await WatermarkOf(verify.Context, workspaceId, ct)).Should().Be("token-2");
    }

    [Fact]
    public async Task Handle_ARevisionArrivingAfterLaterOnes_RebuildsThePeriodsAroundIt()
    {
        // Arrange — the item left for another project and came back: the revision made there
        // arrives through that project's sync, after the later revisions. Without it, revision 4
        // matches revision 2 and merges into its period.
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);
        var day = Duration.FromDays(1);
        await Sync(workspaceId,
        [
            new ExternalRevision(ExternalWorkItemId, 1, Start, "History Story", "New"),
            new ExternalRevision(ExternalWorkItemId, 2, Start.Plus(day), "History Story", "Active"),
            new ExternalRevision(ExternalWorkItemId, 4, Start.Plus(day * 3), "History Story", "Active"),
        ], "token-1", ct);

        // Act
        var written = await Sync(workspaceId,
        [
            new ExternalRevision(ExternalWorkItemId, 3, Start.Plus(day * 2), "History Story", "Blocked"),
        ], "token-2", ct);

        // Assert
        written.Should().Be(4);
        await using var verify = new WaydDbContextAccessor(_fixture);
        var periods = await verify.Context.WorkItemStateHistory.AsNoTracking()
            .Where(h => h.WorkItemId == workItemId)
            .OrderBy(h => h.ValidFrom)
            .ToListAsync(ct);
        periods.Select(p => (p.Revision, p.StatusName, p.ValidFrom, p.ValidTo)).Should().Equal(
            (1, "New", Start, Start.Plus(day)),
            (2, "Active", Start.Plus(day), Start.Plus(day * 2)),
            (3, "Blocked", Start.Plus(day * 2), Start.Plus(day * 3)),
            (4, "Active", Start.Plus(day * 3), (Instant?)null));
    }

    [Fact]
    public async Task Handle_StoresEveryRevisionOnce()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);
        await Sync(workspaceId, Revisions(), "token-1", ct);

        // Act
        await Sync(workspaceId, Revisions(), "token-1", ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var stored = await verify.Context.WorkItemSourceRevisions.AsNoTracking()
            .Where(r => r.WorkItemId == workItemId)
            .OrderBy(r => r.Revision)
            .Select(r => new { r.Revision, r.StatusName })
            .ToListAsync(ct);
        stored.Select(r => (r.Revision, r.StatusName)).Should().Equal(
            (1, "New"), (2, "Active"), (3, "Active"), (4, "Closed"));
    }

    [Fact]
    public async Task Handle_AFill_RebuildsAroundTheMissingRevisionAndKeepsTheWatermark()
    {
        // Arrange — the stream held revisions 1, 2 and 4; revision 3 was made in a project no
        // workspace syncs, so the fill fetches the item's whole history from the item
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);
        var day = Duration.FromDays(1);
        List<IExternalWorkItemRevision> all =
        [
            new ExternalRevision(ExternalWorkItemId, 1, Start, "History Story", "New"),
            new ExternalRevision(ExternalWorkItemId, 2, Start.Plus(day), "History Story", "Active"),
            new ExternalRevision(ExternalWorkItemId, 3, Start.Plus(day * 2), "History Story", "Blocked"),
            new ExternalRevision(ExternalWorkItemId, 4, Start.Plus(day * 3), "History Story", "Active"),
        ];
        await Sync(workspaceId, [all[0], all[1], all[3]], "token-1", ct);

        // Act
        await Sync(workspaceId, all, null, ct, filledWorkItemIds: [ExternalWorkItemId]);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await WatermarkOf(verify.Context, workspaceId, ct)).Should().Be("token-1");
        var periods = await verify.Context.WorkItemStateHistory.AsNoTracking()
            .Where(h => h.WorkItemId == workItemId)
            .OrderBy(h => h.ValidFrom)
            .ToListAsync(ct);
        periods.Select(p => (p.Revision, p.StatusName)).Should().Equal((1, "New"), (2, "Active"), (3, "Blocked"), (4, "Active"));
        periods.Should().OnlyContain(p => p.WorkspaceId == workspaceId);
        var fill = await verify.Context.WorkItemRevisionFills.AsNoTracking().SingleAsync(f => f.WorkItemId == workItemId, ct);
        fill.HighestRevision.Should().Be(4);
    }

    [Fact]
    public async Task Handle_AFillThatReturnedNothing_StillRecordsTheItemAsFilled()
    {
        // Arrange — the source would not return the item's revisions
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);
        await Sync(workspaceId, Revisions().Skip(1).ToList(), "token-1", ct);

        // Act
        await Sync(workspaceId, [], null, ct, filledWorkItemIds: [ExternalWorkItemId]);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var fill = await verify.Context.WorkItemRevisionFills.AsNoTracking().SingleAsync(f => f.WorkItemId == workItemId, ct);
        fill.HighestRevision.Should().Be(4);
        (await WatermarkOf(verify.Context, workspaceId, ct)).Should().Be("token-1");
    }

    [Fact]
    public async Task Handle_AnItemWithAVeryLongHistory_IsStoredInOneBatch()
    {
        // Arrange — a fill brings an item's whole history at once; an item edited by automation can
        // have thousands of revisions, more than SQL Server takes as separate parameters
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);
        await Sync(workspaceId, Revisions().Take(1).ToList(), "token-1", ct);
        var revisions = Enumerable.Range(1, 2_500)
            .Select(rev => (IExternalWorkItemRevision)new ExternalRevision(ExternalWorkItemId, rev, Start.Plus(Duration.FromMinutes(rev)), "History Story", rev % 2 == 0 ? "Active" : "New"))
            .ToList();

        // Act
        await Sync(workspaceId, revisions, null, ct, filledWorkItemIds: [ExternalWorkItemId]);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItemSourceRevisions.CountAsync(r => r.WorkItemId == workItemId, ct)).Should().Be(2_500);
        (await verify.Context.WorkItemStateHistory.CountAsync(h => h.WorkItemId == workItemId, ct)).Should().Be(2_500);
    }

    [Fact]
    public async Task Handle_RevisionsOfAnItemNotInWayd_AreSkippedAndTheWatermarkStillMoves()
    {
        // Arrange — the item was deleted, so the work item sync removed it before history ran
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, _) = await Seed(ct);
        var revisions = new List<IExternalWorkItemRevision>
        {
            new ExternalRevision(999, 1, Start, "History Story", "New"),
        };

        // Act
        await Sync(workspaceId, revisions, "token-1", ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItemStateHistory.AnyAsync(ct)).Should().BeFalse();
        (await WatermarkOf(verify.Context, workspaceId, ct)).Should().Be("token-1");
    }

    [Fact]
    public async Task Handle_ResolvesTheStatusAndKeepsTheSourceValues()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, _) = await Seed(ct);
        var revisions = new List<IExternalWorkItemRevision>
        {
            new ExternalRevision(ExternalWorkItemId, 1, Start, "History Story", "History Status")
            {
                IterationId = 77,
                AssignedTo = new UserRef("unmapped-identity"),
                StoryPoints = 5,
            },
        };

        // Act
        await Sync(workspaceId, revisions, "token-1", ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var period = await verify.Context.WorkItemStateHistory.AsNoTracking().SingleAsync(ct);
        var statusId = await verify.Context.WorkStatuses.Where(s => s.Name == "History Status").Select(s => s.Id).SingleAsync(ct);
        period.StatusId.Should().Be(statusId);
        period.StatusName.Should().Be("History Status");
        period.IterationId.Should().BeNull("iteration 77 is outside the synced set");
        period.ExternalIterationId.Should().Be(77);
        period.AssignedToId.Should().BeNull();
        period.AssignedToExternalId.Should().Be("unmapped-identity");
        period.StoryPoints.Should().Be(5);
    }

    [Fact]
    public async Task DeleteExternalWorkItems_DeletesTheItemsHistory()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);
        await Sync(workspaceId, Revisions(), "token-1", ct);

        // Act
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var handler = new DeleteExternalWorkItemsCommandHandler(accessor.Context, NullLogger<SyncExternalWorkItemsCommandHandler>.Instance);
            var result = await handler.Handle(new DeleteExternalWorkItemsCommand(workspaceId, [ExternalWorkItemId]), ct);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItems.AnyAsync(w => w.Id == workItemId, ct)).Should().BeFalse();
        (await verify.Context.WorkItemStateHistory.AnyAsync(h => h.WorkItemId == workItemId, ct)).Should().BeFalse();
        (await verify.Context.WorkItemSourceRevisions.AnyAsync(r => r.WorkItemId == workItemId, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AFullReplayOntoStoredHistory_ChangesNothing()
    {
        // Arrange — what a full sync does: replay every batch from the start onto what is stored
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, _) = await Seed(ct);
        await Sync(workspaceId, Revisions().Take(2).ToList(), "token-1", ct);
        await Sync(workspaceId, Revisions().Skip(2).ToList(), "token-2", ct);
        var before = await Snapshot(ct);

        // Act
        await Sync(workspaceId, Revisions().Take(2).ToList(), "token-1", ct);
        await Sync(workspaceId, Revisions().Skip(2).ToList(), "token-2", ct);

        // Assert
        (await Snapshot(ct)).Should().Equal(before);
    }

    [Fact]
    public async Task Handle_RevisionsOfAnItemThatMovedToAnotherWorkspace_AreApplied()
    {
        // Arrange — the item now lives in its seeded workspace, but its earlier revisions arrive
        // through the sync of the workspace it moved from, in the same source system
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (currentWorkspaceId, workItemId) = await Seed(ct);
        var formerWorkspaceId = await SeedSiblingWorkspace(currentWorkspaceId, ct);

        // Act
        await Sync(formerWorkspaceId, Revisions(), "token-1", ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var periods = await verify.Context.WorkItemStateHistory.AsNoTracking()
            .Where(h => h.WorkItemId == workItemId)
            .ToListAsync(ct);
        periods.Should().HaveCount(3);
        periods.Should().OnlyContain(p => p.WorkspaceId == formerWorkspaceId);
    }

    /// <summary>
    /// New, Active, a title edit that changes no tracked field, then Closed.
    /// </summary>
    private static List<IExternalWorkItemRevision> Revisions() =>
    [
        new ExternalRevision(ExternalWorkItemId, 1, Start, "History Story", "New"),
        new ExternalRevision(ExternalWorkItemId, 2, Start.Plus(Duration.FromDays(1)), "History Story", "Active"),
        new ExternalRevision(ExternalWorkItemId, 3, Start.Plus(Duration.FromDays(2)), "History Story", "Active"),
        new ExternalRevision(ExternalWorkItemId, 4, Start.Plus(Duration.FromDays(3)), "History Story", "Closed"),
    ];

    /// <returns>The number of periods the batch opened.</returns>
    private async Task<int> Sync(Guid workspaceId, IReadOnlyList<IExternalWorkItemRevision> revisions, string? watermark, CancellationToken ct, IReadOnlyCollection<int>? filledWorkItemIds = null, bool isLastBatch = false, Instant? now = null)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.Now).Returns(now ?? SyncedAt);
        var handler = new SyncExternalWorkItemHistoryCommandHandler(accessor.Context, dateTimeProvider.Object, NullLogger<SyncExternalWorkItemHistoryCommandHandler>.Instance);

        var result = await handler.Handle(new SyncExternalWorkItemHistoryCommand(ConnectionId, workspaceId, revisions, watermark, isLastBatch, filledWorkItemIds), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }

    private async Task<List<(Guid, int, Instant, Instant?, string)>> Snapshot(CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var periods = await accessor.Context.WorkItemStateHistory.AsNoTracking()
            .OrderBy(h => h.WorkItemId).ThenBy(h => h.Revision)
            .ToListAsync(ct);
        return [.. periods.Select(p => (p.WorkItemId, p.Revision, p.ValidFrom, p.ValidTo, p.StatusName))];
    }

    private static Task<string?> WatermarkOf(WaydDbContext context, Guid workspaceId, CancellationToken ct) =>
        context.Workspaces.Where(w => w.Id == workspaceId).Select(w => w.WorkItemHistoryWatermark).SingleAsync(ct);

    private static Task<Instant?> BackfilledOnOf(WaydDbContext context, Guid workspaceId, CancellationToken ct) =>
        context.Workspaces.Where(w => w.Id == workspaceId).Select(w => w.WorkItemHistoryBackfilledOn).SingleAsync(ct);

    private async Task<(Guid WorkspaceId, Guid WorkItemId)> Seed(CancellationToken ct)
    {
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "HIST", ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, ExternalWorkItemId, ct);
        return (workspaceId, workItemId);
    }

    private Task<Guid> SeedSiblingWorkspace(Guid seededWorkspaceId, CancellationToken ct) =>
        WorkItemHistorySeeder.SeedSiblingWorkspace(_fixture, seededWorkspaceId, "HIST2", ct);

    private sealed record ExternalRevision(int WorkItemId, int Revision, Instant Changed, string WorkType, string WorkStatus) : IExternalWorkItemRevision
    {
        public int? IterationId { get; init; }
        public string? TeamKey { get; init; }
        public IExternalUserRef? AssignedTo { get; init; }
        public double? StoryPoints { get; init; }
        public double? Effort { get; init; }
        public double? Size { get; init; }
    }

    private sealed record UserRef(string ExternalId) : IExternalUserRef
    {
        public string? Email => null;
        public string? DisplayName => null;
        public string? Handle => null;
    }
}
