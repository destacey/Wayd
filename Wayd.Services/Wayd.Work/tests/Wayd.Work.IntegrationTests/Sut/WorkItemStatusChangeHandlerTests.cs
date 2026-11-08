using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItems.EventHandlers;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the history is re-stamped set-based, which an in-memory fake cannot
/// execute, and the items are selected by the columns the migration added.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class WorkItemStatusChangeHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "status-change-test";
    private static readonly Instant Start = Instant.FromUtc(2026, 3, 2, 9, 0);
    private static readonly Instant Done = Instant.FromUtc(2026, 3, 9, 9, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_Reclassified_ReappliesTheCategoryToItemsAndHistory()
    {
        // Arrange — "Resolved" was Done, and an administrator makes it Active
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workflow) = await Seed(ct);
        var resolved = workflow.Status("Resolved");
        var closed = workflow.Status("Closed");
        var resolvedItem = await SeedItem(workspaceId, 1, resolved, ct);
        var closedItem = await SeedItem(workspaceId, 2, closed, ct);
        await SeedPeriod(resolvedItem, workspaceId, resolved, ct);
        await SeedPeriod(closedItem, workspaceId, closed, ct);
        await SetStatus(resolved.Id, "Resolved", "Active", ct);

        // Act
        await Handle(new WorkflowStatusReclassifiedEvent(workflow.Id, resolved.Id, "Resolved", workflow.OwnerType,
            StatusCategory.Done, StatusCategory.Active, EventActor.System, Done), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var items = await verify.Context.WorkItems.AsNoTracking().ToDictionaryAsync(w => w.Id, ct);
        items[resolvedItem].StatusCategory.Should().Be(StatusCategory.Active);
        items[resolvedItem].DoneTimestamp.Should().BeNull("an item no longer finished has no done date");
        items[closedItem].StatusCategory.Should().Be(StatusCategory.Done);
        items[closedItem].DoneTimestamp.Should().Be(Done);

        var periods = await verify.Context.WorkItemStateHistory.AsNoTracking().ToDictionaryAsync(h => h.WorkItemId, ct);
        periods[resolvedItem].StatusCategory.Should().Be(StatusCategory.Active);
        periods[closedItem].StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public async Task Handle_Renamed_RenamesTheItemsStatusButNotTheSourcesRecordedName()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workflow) = await Seed(ct);
        var resolved = workflow.Status("Resolved");
        var item = await SeedItem(workspaceId, 1, resolved, ct);
        await SeedPeriod(item, workspaceId, resolved, ct);
        await SetStatus(resolved.Id, "Fixed", "Done", ct);

        // Act
        await Handle(new WorkflowStatusRenamedEvent(workflow.Id, workflow.Key, resolved.Id, "Fixed", null,
            new WorkflowStatusDetails("Resolved", null), EventActor.System, Done), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item, ct);
        saved.StatusName.Should().Be("Fixed");
        saved.StatusCategory.Should().Be(StatusCategory.Done);
        saved.DoneTimestamp.Should().Be(Done);
        var period = await verify.Context.WorkItemStateHistory.AsNoTracking().SingleAsync(h => h.WorkItemId == item, ct);
        period.StatusName.Should().Be("Resolved");
    }

    [Fact]
    public async Task Handle_RenamedOnlyInCase_RenamesTheItemsStatus()
    {
        // Arrange — the database compares names without regard to case, so a filter on stale names would skip this
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workflow) = await Seed(ct);
        var resolved = workflow.Status("Resolved");
        var item = await SeedItem(workspaceId, 1, resolved, ct);
        await SetStatus(resolved.Id, "RESOLVED", "Done", ct);

        // Act
        await Handle(new WorkflowStatusRenamedEvent(workflow.Id, workflow.Key, resolved.Id, "RESOLVED", null,
            new WorkflowStatusDetails("Resolved", null), EventActor.System, Done), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item, ct)).StatusName.Should().Be("RESOLVED");
    }

    [Fact]
    public async Task Handle_AliasChanged_ReappliesTheAlias()
    {
        // Arrange — the item holds an alias its status no longer carries
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workflow) = await Seed(ct);
        var resolved = workflow.Status("Resolved");
        var item = await SeedItem(workspaceId, 1, resolved, ct);
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Work].[WorkItems] SET [StatusAliasValue] = 3 WHERE [Id] = {item};", ct);
        }

        // Act
        await Handle(new WorkflowStatusAliasChangedEvent(workflow.Id, workflow.Key, resolved.Id, 3, StatusWorkflow.NoAlias, EventActor.System, Done), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item, ct)).StatusAliasValue.Should().Be(StatusWorkflow.NoAlias);
    }

    [Fact]
    public async Task Handle_DeliveredTwice_LeavesTheSameState()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workflow) = await Seed(ct);
        var resolved = workflow.Status("Resolved");
        var item = await SeedItem(workspaceId, 1, resolved, ct);
        await SeedPeriod(item, workspaceId, resolved, ct);
        await SetStatus(resolved.Id, "Resolved", "Active", ct);
        var reclassified = new WorkflowStatusReclassifiedEvent(workflow.Id, resolved.Id, "Resolved", workflow.OwnerType,
            StatusCategory.Done, StatusCategory.Active, EventActor.System, Done);
        await Handle(reclassified, ct);

        // Act
        await Handle(reclassified, ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item, ct);
        saved.StatusCategory.Should().Be(StatusCategory.Active);
        saved.StatusName.Should().Be("Resolved");
        saved.DoneTimestamp.Should().BeNull();
        (await verify.Context.WorkItemStateHistory.AsNoTracking().SingleAsync(h => h.WorkItemId == item, ct))
            .StatusCategory.Should().Be(StatusCategory.Active);
    }

    [Fact]
    public async Task Handle_LeavesAnItemOnAnotherStatusAlone()
    {
        // Arrange — the sync moved this item to "Closed" before the reclassification of "Resolved" arrived
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workflow) = await Seed(ct);
        var resolved = workflow.Status("Resolved");
        var moved = await SeedItem(workspaceId, 1, workflow.Status("Closed"), ct);
        await SetStatus(resolved.Id, "Resolved", "Active", ct);

        // Act
        await Handle(new WorkflowStatusReclassifiedEvent(workflow.Id, resolved.Id, "Resolved", workflow.OwnerType,
            StatusCategory.Done, StatusCategory.Active, EventActor.System, Done), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.Id == moved, ct);
        saved.StatusName.Should().Be("Closed");
        saved.StatusCategory.Should().Be(StatusCategory.Done);
        saved.DoneTimestamp.Should().Be(Done);
    }

    [Fact]
    public async Task Handle_ReclassifiedInAnotherOwnerTypesWorkflow_ChangesNothing()
    {
        // Arrange — an item that, against every rule, holds a status of a release workflow
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var (workspaceId, workflow) = await Seed(ct);
        var resolved = workflow.Status("Resolved");
        var item = await SeedItem(workspaceId, 1, resolved, ct);
        await SetStatus(resolved.Id, "Resolved", "Active", ct);
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [StatusWorkflows].[StatusWorkflows] SET [OwnerType] = 'delivery.release' WHERE [Id] = {workflow.Id};", ct);
        }

        // Act
        await Handle(new WorkflowStatusReclassifiedEvent(workflow.Id, resolved.Id, "Resolved", "delivery.release",
            StatusCategory.Done, StatusCategory.Active, EventActor.System, Done), ct);

        // Assert
        await using var verify = new WaydDbContextAccessor(_fixture);
        var saved = await verify.Context.WorkItems.AsNoTracking().SingleAsync(w => w.Id == item, ct);
        saved.StatusCategory.Should().Be(StatusCategory.Done);
    }

    private async Task Handle<TEvent>(TEvent @event, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new WorkItemStatusChangeHandler(accessor.Context, accessor.Context, NullLogger<WorkItemStatusChangeHandler>.Instance);

        var handled = @event switch
        {
            WorkflowStatusReclassifiedEvent e => handler.Handle(e, ct),
            WorkflowStatusRenamedEvent e => handler.Handle(e, ct),
            WorkflowStatusAliasChangedEvent e => handler.Handle(e, ct),
            _ => throw new ArgumentException($"No handler for {typeof(TEvent).Name}."),
        };

        await handled;
    }

    private async Task<(Guid WorkspaceId, StatusWorkflow Workflow)> Seed(CancellationToken ct)
    {
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "CHG", ct);
        var workflow = await WorkItemHistorySeeder.SeedWorkflow(_fixture, workspaceId, [workspaceId],
        [
            new SourceStatus("state-1", "Active", StatusCategory.Active),
            new SourceStatus("state-2", "Resolved", StatusCategory.Done),
            new SourceStatus("state-3", "Closed", StatusCategory.Done),
        ], ct);

        return (workspaceId, workflow);
    }

    /// <summary>Changes the engine's copy of a status, as the workflow's own change saved it.</summary>
    private async Task SetStatus(Guid statusId, string name, string category, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [StatusWorkflows].[WorkflowStatuses] SET [Name] = {name}, [Category] = {category} WHERE [Id] = {statusId};", ct);
    }

    private Task<Guid> SeedItem(Guid workspaceId, int externalId, WorkflowStatus status, CancellationToken ct) =>
        WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, externalId, status, Start,
            status.Category is StatusCategory.Done ? Done : null, ct);

    private Task SeedPeriod(Guid workItemId, Guid workspaceId, WorkflowStatus status, CancellationToken ct) =>
        WorkItemHistorySeeder.SeedPeriod(_fixture, workItemId, workspaceId, 1, Start, null, null, status.Category.ToString(), ct,
            status: status);
}
