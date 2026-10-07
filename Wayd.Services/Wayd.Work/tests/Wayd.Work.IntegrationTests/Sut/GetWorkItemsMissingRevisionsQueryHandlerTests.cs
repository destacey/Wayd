using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the query compares each item's stored revisions in the
/// database, through correlated subqueries an in-memory fake would not translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetWorkItemsMissingRevisionsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "missing-revisions-test";
    private static readonly Instant Start = Instant.FromUtc(2026, 3, 2, 9, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsItemsWithAGapOrNoRevisions_AndNotCompleteOnes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "GAPS", ct);
        var complete = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 101, ct);
        var middleGap = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 102, ct);
        var missingFirst = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 103, ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 104, ct);
        await StoreRevisions(workspaceId, (complete, [1, 2, 3]), (middleGap, [1, 2, 4]), (missingFirst, [3, 4]));

        // Act
        var result = await Query(workspaceId, limit: 10, ct);

        // Assert
        result.Should().Equal(102, 103, 104);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheWorkspacesItemsUpToTheLimit()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "GAPS", ct);
        var otherWorkspaceId = await WorkItemHistorySeeder.SeedSiblingWorkspace(_fixture, workspaceId, "GAPS2", ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 201, ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 202, ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, otherWorkspaceId, 200, ct);

        // Act
        var result = await Query(workspaceId, limit: 1, ct);

        // Assert
        result.Should().Equal(201);
    }

    [Fact]
    public async Task Handle_AnItemFilledAtItsHighestRevision_IsLeftOutUntilANewerOneArrives()
    {
        // Arrange — the fill could not close the gap at revision 4
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "GAPS", ct);
        var stuck = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 301, ct);
        var empty = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 302, ct);
        await StoreRevisions(workspaceId, (stuck, [1, 2, 4]));
        await RecordFill(stuck, 4);
        await RecordFill(empty, 0);
        var before = await Query(workspaceId, limit: 10, ct);

        // Act
        await StoreRevisions(workspaceId, (stuck, [5]));
        var after = await Query(workspaceId, limit: 10, ct);

        // Assert
        before.Should().BeEmpty();
        after.Should().Equal(301);
    }

    private async Task RecordFill(Guid workItemId, int highestRevision)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        accessor.Context.WorkItemRevisionFills.Add(WorkItemRevisionFill.Create(workItemId, highestRevision));
        await accessor.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<int>> Query(Guid workspaceId, int limit, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new GetWorkItemsMissingRevisionsQueryHandler(accessor.Context);

        var result = await handler.Handle(new GetWorkItemsMissingRevisionsQuery(workspaceId, limit), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }

    private async Task StoreRevisions(Guid workspaceId, params (Guid WorkItemId, int[] Revisions)[] items)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var values = new WorkItemSourceValues(null, WorkItemHistorySeeder.StatusName, WorkItemHistorySeeder.WorkTypeName, null, null, null, null, null);
        foreach (var (workItemId, revisions) in items)
        {
            foreach (var revision in revisions)
                accessor.Context.WorkItemSourceRevisions.Add(WorkItemSourceRevision.Create(workItemId, workspaceId, revision, Start.Plus(Duration.FromDays(revision)), values));
        }

        await accessor.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
