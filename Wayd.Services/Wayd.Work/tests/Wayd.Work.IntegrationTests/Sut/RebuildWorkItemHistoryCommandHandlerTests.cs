using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the rebuild deletes periods set-based and replaces them under the unique
/// revision and open-period indexes, which an in-memory fake cannot check.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class RebuildWorkItemHistoryCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "history-rebuild-test";
    private const int ExternalWorkItemId = 601;
    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly Instant Start = Instant.FromUtc(2026, 3, 2, 9, 0);

    private static readonly SourceStatus[] Statuses =
    [
        new("state-1", "New", StatusCategory.Proposed),
        new("state-2", "Active", StatusCategory.Active),
        new("state-3", "Closed", StatusCategory.Done),
    ];

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_RewritesStoredPeriodsWithTheEngineStatus()
    {
        // Arrange — revisions stored before the workspace had a workflow resolved to nothing
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REB", ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, ExternalWorkItemId, ct);
        await SyncHistory(workspaceId, Revisions(ExternalWorkItemId), ct);
        var workflow = await WorkItemHistorySeeder.SeedWorkflow(_fixture, workspaceId, [workspaceId], Statuses, ct);

        // Act
        var written = await Rebuild(workspaceId, ct);

        // Assert
        written.Value.Should().Be(3);
        var periods = await PeriodsOf(workItemId, ct);
        periods.Select(p => (p.Revision, p.StatusId, p.StatusCategory)).Should().Equal(
            (1, workflow.Status("New").Id, StatusCategory.Proposed),
            (2, workflow.Status("Active").Id, StatusCategory.Active),
            (4, workflow.Status("Closed").Id, StatusCategory.Done));
        periods[0].ValidTo.Should().Be(periods[1].ValidFrom);
        periods[^1].ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MoreItemsThanOneBatch_RebuildsEveryItem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REB", ct);
        var externalIds = Enumerable.Range(1, 501).ToList();
        foreach (var externalId in externalIds)
            await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, externalId, ct);
        await SyncHistory(workspaceId, [.. externalIds.Select(id => new ExternalRevision(id, 1, Start, WorkItemHistorySeeder.WorkTypeName, "New"))], ct);
        await WorkItemHistorySeeder.SeedWorkflow(_fixture, workspaceId, [workspaceId], Statuses, ct);

        // Act
        var written = await Rebuild(workspaceId, ct);

        // Assert
        written.Value.Should().Be(501);
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItemStateHistory.CountAsync(h => h.StatusId != null, ct)).Should().Be(501);
    }

    [Fact]
    public async Task Handle_AnItemWithNoStoredRevisions_KeepsItsPeriods()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REB", ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, ExternalWorkItemId, ct);
        await WorkItemHistorySeeder.SeedPeriod(_fixture, workItemId, workspaceId, 1, Start, null, null, "Done", ct);
        await WorkItemHistorySeeder.SeedWorkflow(_fixture, workspaceId, [workspaceId], Statuses, ct);

        // Act
        var written = await Rebuild(workspaceId, ct);

        // Assert
        written.Value.Should().Be(0);
        var period = (await PeriodsOf(workItemId, ct)).Should().ContainSingle().Subject;
        period.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public async Task Handle_AnItemThatMoved_ResolvesEachPeriodInTheWorkspaceThatReadIt()
    {
        // Arrange — the item moved from a workspace whose process calls a state "Design" to one that does not
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var currentWorkspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REB", ct);
        var formerWorkspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REBOLD", ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, currentWorkspaceId, ExternalWorkItemId, ct);
        var former = await WorkItemHistorySeeder.SeedWorkflow(_fixture, formerWorkspaceId, [formerWorkspaceId],
            [new SourceStatus("old-1", "Design", StatusCategory.Proposed)], ct);
        var current = await WorkItemHistorySeeder.SeedWorkflow(_fixture, currentWorkspaceId, [currentWorkspaceId], Statuses, ct);
        await SyncHistory(formerWorkspaceId, [new ExternalRevision(ExternalWorkItemId, 1, Start, WorkItemHistorySeeder.WorkTypeName, "Design")], ct);
        await SyncHistory(currentWorkspaceId, [new ExternalRevision(ExternalWorkItemId, 2, Start.Plus(Duration.FromDays(1)), WorkItemHistorySeeder.WorkTypeName, "Active")], ct);

        // Act
        await Rebuild(currentWorkspaceId, ct);

        // Assert
        var periods = await PeriodsOf(workItemId, ct);
        periods.Select(p => (p.Revision, p.WorkspaceId, p.StatusId, p.StatusCategory)).Should().Equal(
            (1, formerWorkspaceId, former.Status("Design").Id, StatusCategory.Proposed),
            (2, currentWorkspaceId, current.Status("Active").Id, StatusCategory.Active));
    }

    [Fact]
    public async Task Handle_AnItemThatMovedFromADeletedWorkspace_StillResolvesThePeriodsItRead()
    {
        // Arrange — the workspace the item left has since been deleted; its process still names its types
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var currentWorkspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REB", ct);
        var formerWorkspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REBOLD", ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, currentWorkspaceId, ExternalWorkItemId, ct);
        var former = await WorkItemHistorySeeder.SeedWorkflow(_fixture, formerWorkspaceId, [formerWorkspaceId],
            [new SourceStatus("old-1", "Design", StatusCategory.Proposed)], ct);
        var current = await WorkItemHistorySeeder.SeedWorkflow(_fixture, currentWorkspaceId, [currentWorkspaceId], Statuses, ct);
        await SyncHistory(formerWorkspaceId, [new ExternalRevision(ExternalWorkItemId, 1, Start, WorkItemHistorySeeder.WorkTypeName, "Design")], ct);
        await SyncHistory(currentWorkspaceId, [new ExternalRevision(ExternalWorkItemId, 2, Start.Plus(Duration.FromDays(1)), WorkItemHistorySeeder.WorkTypeName, "Active")], ct);
        await using (var context = _fixture.CreateContext())
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Work].[Workspaces] SET [IsDeleted] = 1 WHERE [Id] = {formerWorkspaceId};", ct);
        }

        // Act
        await Rebuild(currentWorkspaceId, ct);

        // Assert
        var periods = await PeriodsOf(workItemId, ct);
        periods.Select(p => (p.Revision, p.WorkspaceId, p.StatusId, p.StatusCategory, p.WorkTypeId.HasValue)).Should().Equal(
            (1, formerWorkspaceId, former.Status("Design").Id, StatusCategory.Proposed, true),
            (2, currentWorkspaceId, current.Status("Active").Id, StatusCategory.Active, true));
    }

    [Fact]
    public async Task Handle_LeavesItemsOfOtherWorkspacesAlone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REB", ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, ExternalWorkItemId, ct);
        await SyncHistory(workspaceId, Revisions(ExternalWorkItemId), ct);
        var otherWorkspaceId = await WorkItemHistorySeeder.SeedSiblingWorkspace(_fixture, workspaceId, "REBOTHER", ct);
        await WorkItemHistorySeeder.SeedWorkflow(_fixture, workspaceId, [workspaceId, otherWorkspaceId], Statuses, ct);

        // Act
        var written = await Rebuild(otherWorkspaceId, ct);

        // Assert
        written.Value.Should().Be(0);
        (await PeriodsOf(workItemId, ct)).Should().HaveCount(3).And.OnlyContain(p => p.StatusId == null);
    }

    [Fact]
    public async Task Handle_AWorkspaceWithNoWorkflow_FailsAndLeavesHistoryAlone()
    {
        // Arrange — the work type is linked to its record type, but the workspace is assigned no scheme
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "REB", ct);
        var workItemId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, ExternalWorkItemId, ct);
        await SyncHistory(workspaceId, Revisions(ExternalWorkItemId), ct);
        await WorkItemHistorySeeder.SeedWorkflow(_fixture, workspaceId, [], Statuses, ct);

        // Act
        var result = await Rebuild(workspaceId, ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        (await PeriodsOf(workItemId, ct)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_AWorkspaceThatDoesNotExist_Fails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);

        // Act
        var result = await Rebuild(Guid.NewGuid(), ct);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    private async Task<Result<int>> Rebuild(Guid workspaceId, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new RebuildWorkItemHistoryCommandHandler(accessor.Context, accessor.Context, new StatusResolver(accessor.Context), NullLogger<RebuildWorkItemHistoryCommandHandler>.Instance);
        return await handler.Handle(new RebuildWorkItemHistoryCommand(ConnectionId, workspaceId), ct);
    }

    private async Task SyncHistory(Guid workspaceId, IReadOnlyList<IExternalWorkItemRevision> revisions, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(p => p.Now).Returns(Start);
        var handler = new SyncExternalWorkItemHistoryCommandHandler(accessor.Context, accessor.Context, new StatusResolver(accessor.Context), dateTimeProvider.Object, NullLogger<SyncExternalWorkItemHistoryCommandHandler>.Instance);

        var result = await handler.Handle(new SyncExternalWorkItemHistoryCommand(ConnectionId, workspaceId, revisions, "token-1"), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
    }

    private async Task<List<Domain.Models.WorkItemStateHistory>> PeriodsOf(Guid workItemId, CancellationToken ct)
    {
        await using var verify = new WaydDbContextAccessor(_fixture);
        return await verify.Context.WorkItemStateHistory.AsNoTracking()
            .Where(h => h.WorkItemId == workItemId)
            .OrderBy(h => h.Revision)
            .ToListAsync(ct);
    }

    /// <summary>New, Active, a revision that changes no tracked field, then Closed.</summary>
    private static List<IExternalWorkItemRevision> Revisions(int workItemId) =>
    [
        new ExternalRevision(workItemId, 1, Start, WorkItemHistorySeeder.WorkTypeName, "New"),
        new ExternalRevision(workItemId, 2, Start.Plus(Duration.FromDays(1)), WorkItemHistorySeeder.WorkTypeName, "Active"),
        new ExternalRevision(workItemId, 3, Start.Plus(Duration.FromDays(2)), WorkItemHistorySeeder.WorkTypeName, "Active"),
        new ExternalRevision(workItemId, 4, Start.Plus(Duration.FromDays(3)), WorkItemHistorySeeder.WorkTypeName, "Closed"),
    ];

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
