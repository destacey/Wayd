using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="GetProjectTaskQueryHandler"/> against a real SQL Server container, where
/// the task's deliverable ids are projected in SQL.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectTaskQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public GetProjectTaskQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
        MapsterConfiguration.Ensure();
    }

    [Fact]
    public async Task Handle_ReturnsTheDeliverablesTheTaskServes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        await using var context = _fixture.CreateContext();
        var handler = new GetProjectTaskQueryHandler(context);

        // Act
        var task = await handler.Handle(new GetProjectTaskQuery(plan.BuildId.ToString()), ct);

        // Assert
        task.Should().NotBeNull();
        task!.DeliverableIds.Should().BeEquivalentTo([plan.PortalId, plan.ReportsId]);
    }
}
