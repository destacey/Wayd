using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Commands;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.Application.WorkProcesses.Commands;
using Wayd.Work.Domain;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Syncs a work process twice, through the real engine commands, and the work items and history that follow it:
/// a state renamed, a state removed and a work type added at the source between the two.
/// </summary>
/// <remarks>
/// Each sync saves several aggregates through separate commands, finding each by its source key, and the item and
/// history syncs read what they saved through the process's scheme and the workspace's assignment, so only a real
/// database shows the pieces agree.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class SyncExternalWorkProcessWorkflowsCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "work-process-sync-test";
    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly Instant Created = Instant.FromUtc(2026, 3, 2, 9, 0);
    private static readonly Instant Changed = Instant.FromUtc(2026, 3, 9, 9, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_AStateRenamedAtTheSource_RenamesTheStatus_AndItemsAndHistoryKeepIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (process, workspaceId, bug) = await SeedProcess(ct);
        await SyncProcess(process.With(bug), ct);
        await SyncItem(workspaceId, Item(801, bug.Name, "Active"), ct);
        var activeId = await StatusIdOf(801, ct);

        // Act
        await SyncProcess(process.With(bug.Renaming("active", "Doing")), ct);
        await SyncItem(workspaceId, Item(801, bug.Name, "Doing", Changed), ct);
        await SyncHistory(workspaceId, [Revision(801, 1, Created, bug.Name, "Active"), Revision(801, 2, Changed, bug.Name, "Doing")], ct);

        // Assert
        var status = await StatusOf(activeId, ct);
        (status.Name, status.IsRetired).Should().Be(("Doing", false));
        status.FormerNames.Should().Equal("Active");
        (await StatusIdOf(801, ct)).Should().Be(activeId, "the item keeps the status its state was renamed under");
        (await HistoryOf(801, ct)).Should().Equal(
            (1, "Active", activeId, StatusCategory.Active),
            (2, "Doing", activeId, StatusCategory.Active));
    }

    [Fact]
    public async Task Handle_AStateRemovedAtTheSource_RetiresTheStatus_AndItemsHoldingItStillResolve()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (process, workspaceId, bug) = await SeedProcess(ct);
        await SyncProcess(process.With(bug), ct);
        await SyncItem(workspaceId, Item(802, bug.Name, "Closed"), ct);
        var closedId = await StatusIdOf(802, ct);

        // Act
        await SyncProcess(process.With(bug.Without("closed")), ct);
        await SyncItem(workspaceId, Item(802, bug.Name, "Closed", Changed, title: "Retitled"), ct);
        await SyncHistory(workspaceId, [Revision(802, 1, Created, bug.Name, "Closed")], ct);

        // Assert
        var status = await StatusOf(closedId, ct);
        (status.Name, status.IsRetired).Should().Be(("Closed", true));
        await using var verify = new WaydDbContextAccessor(_fixture);
        var item = await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.ExternalId == 802, ct);
        (item.Title, item.StatusId, item.StatusCategory).Should().Be(("Retitled", closedId, StatusCategory.Done));
        (await HistoryOf(802, ct)).Should().Equal((1, "Closed", closedId, StatusCategory.Done));
    }

    [Fact]
    public async Task Handle_AWorkTypeAddedAtTheSource_BecomesARecordTypeWithItsWorkflow_OnTheNextSync()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (process, workspaceId, bug) = await SeedProcess(ct);
        await SyncProcess(process.With(bug), ct);
        var story = new TestWorkType($"Sync Story {Guid.NewGuid():N}"[..24], "Custom.Story",
            [new TestState("story-new", "New", StatusCategory.Proposed, 1, true)]);

        // Act
        await SyncProcess(process.With(bug, story), ct);
        await SyncItem(workspaceId, Item(803, story.Name, "New"), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var context = verify.Context;
        var ownerType = WorkWorkflowOwners.WorkItem.Key;
        var workType = await context.WorkTypes.AsNoTracking().SingleAsync(t => t.Name == story.Name, ct);
        var recordType = await context.RecordTypes.AsNoTracking().SingleAsync(r => r.OwnerType == ownerType && r.ExternalKey == story.Name, ct);
        workType.RecordTypeId.Should().Be(recordType.Id);

        var workflow = await context.StatusWorkflows.AsNoTracking().Include(w => w.Statuses)
            .SingleAsync(w => w.OwnerType == ownerType && w.ExternalKey == $"work-process:{process.Id:D}:Custom.Story", ct);
        workflow.Statuses.Select(s => s.Name).Should().Equal("New");
        var scheme = await context.StatusWorkflowSchemes.AsNoTracking().Include(s => s.Entries)
            .SingleAsync(s => s.OwnerType == ownerType && s.ExternalKey == $"work-process:{process.Id:D}", ct);
        scheme.WorkflowFor(recordType.Id).Should().Be(workflow.Id);

        var item = await context.WorkItems.AsNoTracking().SingleAsync(w => w.ExternalId == 803, ct);
        (item.TypeId, item.StatusId).Should().Be((workType.Id, workflow.Statuses.Single().Id));
    }

    [Fact]
    public async Task Handle_GivesANewStatusTheDescriptionItsNameHadInWork_AndRecordsIt()
    {
        // Arrange — a status name of this test's own: the kept descriptions outlive every reset
        var ct = TestContext.Current.CancellationToken;
        var (process, _, bug) = await SeedProcess(ct);
        var triage = $"Triage {Guid.NewGuid():N}"[..20];
        await using (var seed = _fixture.CreateContext())
        {
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [Work].[LegacyWorkStatusDescriptions] ([Name], [Description]) VALUES ({triage.ToUpperInvariant()}, 'Waiting to be looked at.');", ct);
        }

        // Act
        await SyncProcess(process.With(bug with { WorkflowStates = [.. bug.WorkflowStates, new TestState("triage", triage, StatusCategory.Proposed, 4, true)] }), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var workflow = await verify.Context.StatusWorkflows.AsNoTracking().Include(w => w.Statuses)
            .SingleAsync(w => w.ExternalKey == $"work-process:{process.Id:D}:Custom.Bug", ct);
        var status = workflow.Statuses.Single(s => s.Name == triage);
        status.Description.Should().Be("Waiting to be looked at.", "the name is matched without regard to case");
        workflow.Statuses.Where(s => s.Name != triage).Should().OnlyContain(s => s.Description == null);
        // Saved with the status, so the event that brings the status in (the workflow's creation here) records it.
        var logged = await verify.Context.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == workflow.Id)
            .Select(a => new { a.EventType, a.Payload })
            .ToListAsync(ct);
        logged.Should().Contain(a => a.Payload.Contains(triage) && a.Payload.Contains("Waiting to be looked at."));
        logged.Should().NotContain(a => a.EventType == "WorkflowStatusDescriptionChangedEvent");
    }

    [Fact]
    public async Task Handle_LeavesADescriptionAnAdministratorCleared_Cleared()
    {
        // Arrange — the first sync gives the new status its shared description, which an administrator clears
        var ct = TestContext.Current.CancellationToken;
        var (process, _, bug) = await SeedProcess(ct);
        var triage = $"Triage {Guid.NewGuid():N}"[..20];
        await using (var seed = _fixture.CreateContext())
        {
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [Work].[LegacyWorkStatusDescriptions] ([Name], [Description]) VALUES ({triage}, 'Waiting to be looked at.');", ct);
        }
        var synced = process.With(bug with { WorkflowStates = [.. bug.WorkflowStates, new TestState("triage", triage, StatusCategory.Proposed, 4, true)] });
        await SyncProcess(synced, ct);
        await using (var edit = new WaydDbContextAccessor(_fixture))
        {
            var workflow = await edit.Context.StatusWorkflows.Include(w => w.Statuses)
                .SingleAsync(w => w.ExternalKey == $"work-process:{process.Id:D}:Custom.Bug", ct);
            var status = workflow.Statuses.Single(s => s.Name == triage);
            status.Description.Should().Be("Waiting to be looked at.");
            workflow.DescribeStatus(status.Id, null, EventActor.User(Guid.NewGuid().ToString()), SqlServerDbContextFixture.FixedNow)
                .IsSuccess.Should().BeTrue();
            await edit.Context.SaveChangesAsync(ct);
        }

        // Act
        await SyncProcess(synced, ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var reread = await verify.Context.StatusWorkflows.AsNoTracking().Include(w => w.Statuses)
            .SingleAsync(w => w.ExternalKey == $"work-process:{process.Id:D}:Custom.Bug", ct);
        reread.Statuses.Single(s => s.Name == triage).Description.Should().BeNull("only a status the sync creates is given the shared description");
    }

    /// <summary>
    /// A managed process with one workspace, and a "Bug" type of a name of its own whose states are New, Active
    /// and Closed.
    /// </summary>
    private async Task<(TestProcess Process, Guid WorkspaceId, TestWorkType Bug)> SeedProcess(CancellationToken ct)
    {
        await _fixture.ResetWorkData(ct);
        WorkWorkflowOwners.Register();

        await using var accessor = new WaydDbContextAccessor(_fixture);
        var context = accessor.Context;

        // New work types are created at the "Other" level, which only a hierarchy has.
        if (!await context.WorkTypeHierarchies.AnyAsync(ct))
            context.WorkTypeHierarchies.Add(WorkTypeHierarchy.Initialize(SqlServerDbContextFixture.FixedNow));

        var process = new TestProcess(Guid.NewGuid(), $"Sync Process {Guid.NewGuid():N}"[..24]);
        var workProcess = WorkProcess.CreateExternal(process.Name, null, process.Id, SqlServerDbContextFixture.FixedNow);
        context.WorkProcesses.Add(workProcess);
        await context.SaveChangesAsync(ct);

        var workspace = Workspace.CreateExternal(new WorkspaceKey("SYNC"), "Sync Workspace", null,
            OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, SystemId, Guid.NewGuid().ToString()),
            workProcess.Id, null, SqlServerDbContextFixture.FixedNow);
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync(ct);

        var bug = new TestWorkType($"Sync Bug {Guid.NewGuid():N}"[..22], "Custom.Bug",
        [
            new TestState("new", "New", StatusCategory.Proposed, 1, true),
            new TestState("active", "Active", StatusCategory.Active, 2, true),
            new TestState("closed", "Closed", StatusCategory.Done, 3, true),
        ]);

        return (process, workspace.Id, bug);
    }

    /// <summary>Syncs the process with the real engine commands, sharing one context as the dispatcher's scope does.</summary>
    private async Task SyncProcess(IExternalWorkProcessConfiguration process, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var context = accessor.Context;
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.Now).Returns(Changed);

        var dispatcher = new Mock<IDispatcher>();
        dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedRecordTypeCommand>(), It.IsAny<CancellationToken>()))
            .Returns((SyncManagedRecordTypeCommand c, CancellationToken t) =>
                new SyncManagedRecordTypeCommandHandler(context, dateTimeProvider.Object, NullLogger<SyncManagedRecordTypeCommandHandler>.Instance).Handle(c, t));
        dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .Returns((SyncManagedWorkflowCommand c, CancellationToken t) =>
                new SyncManagedWorkflowCommandHandler(context, dateTimeProvider.Object, NullLogger<SyncManagedWorkflowCommandHandler>.Instance).Handle(c, t));
        dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowSchemeCommand>(), It.IsAny<CancellationToken>()))
            .Returns((SyncManagedWorkflowSchemeCommand c, CancellationToken t) =>
                new SyncManagedWorkflowSchemeCommandHandler(context, dateTimeProvider.Object, NullLogger<SyncManagedWorkflowSchemeCommandHandler>.Instance).Handle(c, t));
        dispatcher.Setup(d => d.Send(It.IsAny<SyncManagedWorkflowAssignmentCommand>(), It.IsAny<CancellationToken>()))
            .Returns((SyncManagedWorkflowAssignmentCommand c, CancellationToken t) =>
                new SyncManagedWorkflowAssignmentCommandHandler(context, dateTimeProvider.Object, NullLogger<SyncManagedWorkflowAssignmentCommandHandler>.Instance).Handle(c, t));

        var handler = new SyncExternalWorkProcessWorkflowsCommandHandler(context, context, dispatcher.Object, dateTimeProvider.Object,
            NullLogger<SyncExternalWorkProcessWorkflowsCommandHandler>.Instance);

        var result = await handler.Handle(new SyncExternalWorkProcessWorkflowsCommand(process), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private async Task SyncItem(Guid workspaceId, IExternalWorkItem workItem, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.Now).Returns(Changed);
        var handler = new SyncExternalWorkItemsCommandHandler(accessor.Context, accessor.Context, new StatusResolver(accessor.Context), dateTimeProvider.Object, NullLogger<SyncExternalWorkItemsCommandHandler>.Instance);

        var result = await handler.Handle(new SyncExternalWorkItemsCommand(ConnectionId, Connector.AzureDevOps, workspaceId, [workItem], [], []), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private async Task SyncHistory(Guid workspaceId, List<IExternalWorkItemRevision> revisions, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.Now).Returns(Changed);
        var handler = new SyncExternalWorkItemHistoryCommandHandler(accessor.Context, accessor.Context, new StatusResolver(accessor.Context), dateTimeProvider.Object, NullLogger<SyncExternalWorkItemHistoryCommandHandler>.Instance);

        var result = await handler.Handle(new SyncExternalWorkItemHistoryCommand(ConnectionId, workspaceId, revisions, "token-1", false, null), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private async Task<Guid> StatusIdOf(int externalId, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        return await accessor.Context.WorkItems.Where(w => w.ExternalId == externalId).Select(w => w.StatusId).SingleAsync(ct);
    }

    private async Task<WorkflowStatus> StatusOf(Guid statusId, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var workflow = await accessor.Context.StatusWorkflows.AsNoTracking().Include(w => w.Statuses)
            .SingleAsync(w => w.Statuses.Any(s => s.Id == statusId), ct);
        return workflow.Statuses.Single(s => s.Id == statusId);
    }

    private async Task<List<(int Revision, string StatusName, Guid? StatusId, StatusCategory? Category)>> HistoryOf(int externalId, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var context = accessor.Context;
        var workItemId = await context.WorkItems.Where(w => w.ExternalId == externalId).Select(w => w.Id).SingleAsync(ct);
        var periods = await context.WorkItemStateHistory.AsNoTracking()
            .Where(h => h.WorkItemId == workItemId)
            .OrderBy(h => h.Revision)
            .Select(h => new { h.Revision, h.StatusName, h.StatusId, h.StatusCategory })
            .ToListAsync(ct);
        return [.. periods.Select(p => (p.Revision, p.StatusName, p.StatusId, p.StatusCategory))];
    }

    private static ExternalWorkItem Item(int id, string workType, string status, Instant? lastModified = null, string title = "Synced item") =>
        new(id, title, workType, status, Created, lastModified ?? Created);

    private static ExternalRevision Revision(int workItemId, int revision, Instant changed, string workType, string status) =>
        new(workItemId, revision, changed, workType, status);

    /// <summary>The source process, which each sync reports with the work types it has then.</summary>
    private sealed record TestProcess(Guid Id, string Name)
    {
        public IExternalWorkProcessConfiguration With(params TestWorkType[] workTypes)
        {
            var process = new Mock<IExternalWorkProcessConfiguration>();
            process.Setup(p => p.Id).Returns(Id);
            process.Setup(p => p.Name).Returns(Name);
            process.Setup(p => p.WorkTypes).Returns(workTypes.Cast<IExternalWorkTypeWorkflow>().ToList());
            return process.Object;
        }
    }

    private sealed record TestWorkType(string Name, string ExternalKey, IList<IExternalWorkflowState> WorkflowStates) : IExternalWorkTypeWorkflow
    {
        public string? Description => null;
        public string WorkTypeLevelId => "level";
        public bool IsActive => true;

        /// <summary>The type with the state keyed <paramref name="externalKey"/> renamed.</summary>
        public TestWorkType Renaming(string externalKey, string name) =>
            this with { WorkflowStates = [.. WorkflowStates.Select(s => s.ExternalKey == externalKey ? ((TestState)s) with { StatusName = name } : s)] };

        /// <summary>The type without the state keyed <paramref name="externalKey"/>, as when the source removes it.</summary>
        public TestWorkType Without(string externalKey) =>
            this with { WorkflowStates = [.. WorkflowStates.Where(s => s.ExternalKey != externalKey)] };
    }

    private sealed record TestState(string ExternalKey, string StatusName, StatusCategory Category, int Order, bool IsActive) : IExternalWorkflowState;

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
