using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Domain.Interfaces;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because moving an item between workspaces depends on what the
/// database and change tracker do: the item is found through its old workspace's system id, its
/// key changes under a unique index, and the history rows keyed to its id must survive.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SyncExternalWorkItemsCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "work-item-move-test";
    private const string WorkTypeName = WorkItemHistorySeeder.WorkTypeName;
    private const string NewStatusName = "Move New";
    private const string ActiveStatusName = "Move Active";
    private const int ExternalWorkItemId = 701;
    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly Instant Created = Instant.FromUtc(2026, 3, 2, 9, 0);
    private static readonly Instant MovedAt = Instant.FromUtc(2026, 3, 9, 9, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_AnItemThatMovedFromAnotherWorkspace_MovesItKeepingItsIdAndHistory()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (fromWorkspaceId, toWorkspaceId) = await SeedTwoWorkspaces(ct);
        await Sync(fromWorkspaceId, Item(NewStatusName, Created), ct);
        var workItemId = await IdOf(ExternalWorkItemId, ct);
        await SyncHistory(fromWorkspaceId, ct);

        // Act
        await Sync(toWorkspaceId, Item(ActiveStatusName, MovedAt, title: "Moved title"), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var items = await verify.Context.WorkItems.AsNoTracking()
            .Where(w => w.ExternalId == ExternalWorkItemId)
            .ToListAsync(ct);
        var item = items.Should().ContainSingle().Subject;
        item.Id.Should().Be(workItemId);
        item.WorkspaceId.Should().Be(toWorkspaceId);
        item.Key.ToString().Should().Be($"MOVETO-{ExternalWorkItemId}");
        item.Title.Should().Be("Moved title");
        item.StatusCategory.Should().Be(StatusCategory.Active);
        item.LastModified.Should().Be(MovedAt);

        var history = await verify.Context.WorkItemStateHistory.AsNoTracking()
            .Where(h => h.WorkItemId == workItemId)
            .ToListAsync(ct);
        history.Should().ContainSingle().Which.WorkspaceId.Should().Be(fromWorkspaceId);
        (await verify.Context.WorkItemSourceRevisions.CountAsync(r => r.WorkItemId == workItemId, ct)).Should().Be(1);
    }

    [Fact]
    public async Task Handle_TheOldWorkspaceSyncingAfterTheMove_LeavesTheItemInTheNewWorkspace()
    {
        // Arrange — the old project's next sync no longer returns the item, but other items it
        // still holds go through the handler
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (fromWorkspaceId, toWorkspaceId) = await SeedTwoWorkspaces(ct);
        await Sync(fromWorkspaceId, Item(NewStatusName, Created), ct);
        await Sync(toWorkspaceId, Item(ActiveStatusName, MovedAt), ct);

        // Act
        await Sync(fromWorkspaceId, Item(NewStatusName, MovedAt, id: ExternalWorkItemId + 1), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var moved = await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.ExternalId == ExternalWorkItemId, ct);
        moved.WorkspaceId.Should().Be(toWorkspaceId);
        var other = await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.ExternalId == ExternalWorkItemId + 1, ct);
        other.WorkspaceId.Should().Be(fromWorkspaceId);
    }

    private async Task Sync(Guid workspaceId, IExternalWorkItem workItem, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.Now).Returns(MovedAt);
        var handler = new SyncExternalWorkItemsCommandHandler(accessor.Context, dateTimeProvider.Object, NullLogger<SyncExternalWorkItemsCommandHandler>.Instance);

        var result = await handler.Handle(new SyncExternalWorkItemsCommand(ConnectionId, Connector.AzureDevOps, workspaceId, [workItem], [], []), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private async Task SyncHistory(Guid workspaceId, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.Now).Returns(MovedAt);
        var handler = new SyncExternalWorkItemHistoryCommandHandler(accessor.Context, dateTimeProvider.Object, NullLogger<SyncExternalWorkItemHistoryCommandHandler>.Instance);

        var revisions = new List<IExternalWorkItemRevision> { new ExternalRevision(ExternalWorkItemId, 1, Created, WorkTypeName, NewStatusName) };
        var result = await handler.Handle(new SyncExternalWorkItemHistoryCommand(ConnectionId, workspaceId, revisions, "token-1", false, null), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private async Task<Guid> IdOf(int externalId, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        return await accessor.Context.WorkItems.Where(w => w.ExternalId == externalId).Select(w => w.Id).SingleAsync(ct);
    }

    /// <summary>
    /// Two managed workspaces in one source system sharing a process whose one work type maps
    /// <see cref="NewStatusName"/> to Proposed and <see cref="ActiveStatusName"/> to Active.
    /// </summary>
    private async Task<(Guid From, Guid To)> SeedTwoWorkspaces(CancellationToken ct)
    {
        // A work type level is created only through SQL: its domain factory is internal.
        await WorkItemHistorySeeder.EnsureWorkType(_fixture, ct);

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var context = accessor.Context;

        var workTypeId = await context.WorkTypes.Where(t => t.Name == WorkTypeName).Select(t => t.Id).SingleAsync(ct);
        var newStatus = await StatusNamed(NewStatusName);
        var activeStatus = await StatusNamed(ActiveStatusName);

        var workflow = Workflow.CreateExternal("Move Workflow", null,
        [
            new WorkflowSchemeInput(newStatus.Id, StatusCategory.Proposed, 1),
            new WorkflowSchemeInput(activeStatus.Id, StatusCategory.Active, 2),
        ]);
        context.Workflows.Add(workflow);
        var workProcess = WorkProcess.CreateExternal("Move Process", null, Guid.NewGuid(), SqlServerDbContextFixture.FixedNow);
        context.WorkProcesses.Add(workProcess);
        await context.SaveChangesAsync(ct);

        workProcess.AddWorkType(workTypeId, workflow.Id, true, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();

        var from = ExternalWorkspace("MOVEFROM", workProcess.Id);
        var to = ExternalWorkspace("MOVETO", workProcess.Id);
        context.Workspaces.AddRange(from, to);
        await context.SaveChangesAsync(ct);

        return (from.Id, to.Id);

        async Task<WorkStatus> StatusNamed(string name)
        {
            var status = await context.WorkStatuses.FirstOrDefaultAsync(s => s.Name == name, ct);
            if (status is not null)
                return status;

            status = WorkStatus.Create(name, null, SqlServerDbContextFixture.FixedNow);
            context.WorkStatuses.Add(status);
            await context.SaveChangesAsync(ct);
            return status;
        }
    }

    private static Workspace ExternalWorkspace(string key, Guid workProcessId) =>
        Workspace.CreateExternal(new WorkspaceKey(key), $"{key} Workspace", null,
            OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, SystemId, Guid.NewGuid().ToString()),
            workProcessId, null, SqlServerDbContextFixture.FixedNow);

    private static ExternalWorkItem Item(string status, Instant lastModified, int id = ExternalWorkItemId, string title = "Movable item") =>
        new(id, title, WorkTypeName, status, Created, lastModified);

    private sealed record WorkflowSchemeInput(int WorkStatusId, StatusCategory Category, int Order) : ICreateWorkflowScheme
    {
        public bool IsActive => true;
    }

    private sealed record ExternalWorkItem(int Id, string Title, string WorkType, string WorkStatus, Instant Created, Instant LastModified) : IExternalWorkItem
    {
        public int? ParentId => null;
        public IExternalUserRef? AssignedTo => null;
        public IExternalUserRef? CreatedBy => null;
        public IExternalUserRef? LastModifiedBy => null;
        public int? Priority => null;
        public double StackRank => 1;
        public Instant? ActivatedTimestamp => null;
        public Instant? DoneTimestamp => null;
        public Guid? TeamId { get; set; }
        public string? ExternalTeamIdentifier => null;
        public int? IterationId => null;
        public double? StoryPoints => null;
        public double? Effort => null;
        public double? Size => null;
        public IReadOnlyCollection<string> Tags => [];
    }

    private sealed record ExternalRevision(int WorkItemId, int Revision, Instant Changed, string WorkType, string WorkStatus) : IExternalWorkItemRevision
    {
        public int? IterationId => null;
        public string? TeamKey => null;
        public IExternalUserRef? AssignedTo => null;
        public double? StoryPoints => null;
        public double? Effort => null;
        public double? Size => null;
    }
}
