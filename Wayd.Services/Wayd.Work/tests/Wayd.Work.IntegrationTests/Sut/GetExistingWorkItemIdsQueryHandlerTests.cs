using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Work.Application.WorkItems.Queries;
using Wayd.Work.IntegrationTests.Infrastructure;

namespace Wayd.Work.IntegrationTests.Sut;

/// <summary>
/// Runs against real SQL Server because the answer is read with an IN list over the work item table, which a
/// fake context does not translate.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetExistingWorkItemIdsQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsOnlyTheIdsWorkHas()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetWorkData(ct);
        var workspaceId = await WorkItemHistorySeeder.SeedWorkspace(_fixture, "existing-ids-test", "EXIST", ct);
        var first = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 801, ct);
        var second = await WorkItemHistorySeeder.SeedWorkItem(_fixture, workspaceId, 802, ct);
        var missing = Guid.NewGuid();
        await using var accessor = new WaydDbContextAccessor(_fixture);

        // Act
        var found = await new GetExistingWorkItemIdsQueryHandler(accessor.Context)
            .Handle(new GetExistingWorkItemIdsQuery([first, second, missing, first]), ct);

        // Assert
        found.Should().BeEquivalentTo([first, second]);
    }
}
