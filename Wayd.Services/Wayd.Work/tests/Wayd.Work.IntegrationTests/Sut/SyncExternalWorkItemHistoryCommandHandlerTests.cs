using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_WithRevisions_StoresOnePeriodPerTrackedChangeAndTheWatermark()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workItemId) = await Seed(ct);

        // Act
        await Sync(workspaceId, Revisions(), "token-1", ct);

        // Assert
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
        await Sync(workspaceId, Revisions(), "token-1", ct);

        // Assert
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
    }

    [Fact]
    public async Task ResetThenReplay_ProducesTheSamePeriods()
    {
        // Arrange — what a full sync does
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, _) = await Seed(ct);
        await Sync(workspaceId, Revisions(), "token-1", ct);
        var before = await Snapshot(ct);

        // Act
        await using (var accessor = new WaydDbContextAccessor(_fixture))
        {
            var reset = new ResetWorkItemHistoryCommandHandler(accessor.Context, NullLogger<ResetWorkItemHistoryCommandHandler>.Instance);
            var result = await reset.Handle(new ResetWorkItemHistoryCommand(workspaceId), ct);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }
        await using (var verifyReset = new WaydDbContextAccessor(_fixture))
        {
            (await verifyReset.Context.WorkItemStateHistory.AnyAsync(ct)).Should().BeFalse();
            (await WatermarkOf(verifyReset.Context, workspaceId, ct)).Should().BeNull();
        }
        await Sync(workspaceId, Revisions(), "token-1", ct);

        // Assert
        (await Snapshot(ct)).Should().Equal(before);
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

    private async Task Sync(Guid workspaceId, IReadOnlyList<IExternalWorkItemRevision> revisions, string? watermark, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new SyncExternalWorkItemHistoryCommandHandler(accessor.Context, NullLogger<SyncExternalWorkItemHistoryCommandHandler>.Instance);

        var result = await handler.Handle(new SyncExternalWorkItemHistoryCommand(ConnectionId, workspaceId, revisions, watermark), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
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

    // Straight through SQL: a valid WorkItem needs a workspace, process, type and status that this
    // test never reads through the aggregates.
    private async Task<(Guid WorkspaceId, Guid WorkItemId)> Seed(CancellationToken ct)
    {
        var workProcessId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();
        var workItemId = Guid.CreateVersion7();

        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypeLevels] WHERE [Name] = 'History Level')
                INSERT INTO [Work].[WorkTypeLevels]
                    ([Name], [Tier], [Ownership], [Order], [SystemCreated], [SystemLastModified])
                VALUES ('History Level', 'Requirement', 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkTypes] WHERE [Name] = 'History Story')
                INSERT INTO [Work].[WorkTypes]
                    ([Name], [IsActive], [IsDeleted], [LevelId], [SystemCreated], [SystemLastModified])
                SELECT 'History Story', 1, 0, MAX([Id]), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM [Work].[WorkTypeLevels] WHERE [Name] = 'History Level';

            IF NOT EXISTS (SELECT 1 FROM [Work].[WorkStatuses] WHERE [Name] = 'History Status')
                INSERT INTO [Work].[WorkStatuses]
                    ([Name], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
                VALUES ('History Status', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[WorkProcesses]
                ([Id], [Name], [Ownership], [IsActive], [IsDeleted], [SystemCreated], [SystemLastModified])
            VALUES ({workProcessId}, 'History Process', 'Managed', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            INSERT INTO [Work].[Workspaces]
                ([Id], [Key], [Name], [Ownership], [SystemId], [WorkProcessId], [IsActive], [IsDeleted],
                 [SystemCreated], [SystemLastModified])
            VALUES ({workspaceId}, 'HIST', 'History Workspace', 'Managed', {SystemId}, {workProcessId}, 1, 0,
                    SYSUTCDATETIME(), SYSUTCDATETIME());

            DECLARE @TypeId int = (SELECT TOP 1 [Id] FROM [Work].[WorkTypes] WHERE [Name] = 'History Story');
            DECLARE @StatusId int = (SELECT TOP 1 [Id] FROM [Work].[WorkStatuses] WHERE [Name] = 'History Status');

            INSERT INTO [Work].[WorkItems]
                ([Id], [Key], [Title], [WorkspaceId], [ExternalId], [TypeId], [StatusId],
                 [StatusCategory], [Created], [LastModified], [StackRank],
                 [SystemCreated], [SystemLastModified])
            VALUES ({workItemId}, 'HIST-1', 'History item', {workspaceId}, {ExternalWorkItemId}, @TypeId, @StatusId,
                    'Done', SYSUTCDATETIME(), SYSUTCDATETIME(), 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            ct);

        return (workspaceId, workItemId);
    }

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
