using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Work.Application.WorkItems.Commands;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the delete reaches items through the workspaces it is
/// given and removes them with their links, which a fake context cannot show.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class DeleteExternalWorkItemsCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private const string SystemId = "work-item-delete-test";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_DeletesTheItemsFromWhicheverNamedWorkspaceHoldsThem()
    {
        // Arrange — item 801 still sits in the workspace of the project it left
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var leftWorkspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "DELLEFT", ct);
        var currentWorkspaceId = await WorkItemHistorySeeder.SeedSiblingWorkspace(_fixture, leftWorkspaceId, "DELNOW", ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, leftWorkspaceId, 801, ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, currentWorkspaceId, 802, ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, currentWorkspaceId, 803, ct);

        // Act
        var deleted = await Delete([currentWorkspaceId, leftWorkspaceId], [801, 802], ct);

        // Assert
        deleted.Should().Be(2);
        await using var verify = new WaydDbContextAccessor(_fixture);
        var remaining = await verify.Context.WorkItems.AsNoTracking().Select(w => w.ExternalId).ToListAsync(ct);
        remaining.Should().Equal(803);
    }

    [Fact]
    public async Task Handle_LeavesItemsInWorkspacesItWasNotGiven()
    {
        // Arrange — a workspace the request does not name holds the external id
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var syncedWorkspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "DELMINE", ct);
        var otherWorkspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "DELOTHER", ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, otherWorkspaceId, 801, ct);

        // Act
        var deleted = await Delete([syncedWorkspaceId], [801], ct);

        // Assert
        deleted.Should().Be(0);
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItems.AsNoTracking().SingleAsync(ct)).WorkspaceId.Should().Be(otherWorkspaceId);
    }

    [Fact]
    public async Task Handle_MoreIdsThanOneBatch_DeletesThemAll()
    {
        // Arrange — a full sync can delete thousands, more than one IN list carries
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "DELMANY", ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 1, ct);
        await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 2_400, ct);

        // Act
        var deleted = await Delete([workspaceId], [.. Enumerable.Range(1, 2_500)], ct);

        // Assert
        deleted.Should().Be(2);
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItems.AnyAsync(ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_RemovesTheLinksOfADeletedItemAndKeepsTheItemItLinkedTo()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, SystemId, "DELLINK", ct);
        var deletedId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 801, ct);
        var keptId = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 802, ct);
        await SeedDependency(deletedId, keptId, ct);
        await SeedDependency(keptId, deletedId, ct);

        // Act
        var deleted = await Delete([workspaceId], [801], ct);

        // Assert
        deleted.Should().Be(1);
        await using var verify = new WaydDbContextAccessor(_fixture);
        (await verify.Context.WorkItems.AsNoTracking().SingleAsync(ct)).Id.Should().Be(keptId);
        (await verify.Context.WorkItemDependencies.AnyAsync(ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenNoNamedWorkspaceExists_Fails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new DeleteExternalWorkItemsCommandHandler(accessor.Context, NullLogger<DeleteExternalWorkItemsCommandHandler>.Instance);

        // Act
        var result = await handler.Handle(new DeleteExternalWorkItemsCommand([Guid.NewGuid()], [801]), ct);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    private async Task SeedDependency(Guid sourceId, Guid targetId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [Work].[WorkItemLinks]
                ([Id], [LinkType], [SourceId], [TargetId], [CreatedOn], [SystemCreated], [SystemLastModified])
            VALUES ({Guid.CreateVersion7()}, 'Dependency', {sourceId}, {targetId}, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            ct);
    }

    /// <returns>How many items the handler reports it deleted.</returns>
    private async Task<int> Delete(Guid[] workspaceIds, int[] workItemIds, CancellationToken ct)
    {
        await using var accessor = new WaydDbContextAccessor(_fixture);
        var handler = new DeleteExternalWorkItemsCommandHandler(accessor.Context, NullLogger<DeleteExternalWorkItemsCommandHandler>.Instance);

        var result = await handler.Handle(new DeleteExternalWorkItemsCommand(workspaceIds, workItemIds), ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }
}
