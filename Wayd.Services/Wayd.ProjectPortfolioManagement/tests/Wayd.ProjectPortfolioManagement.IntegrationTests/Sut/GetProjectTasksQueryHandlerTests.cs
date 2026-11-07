using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Queries;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="GetProjectTasksQueryHandler"/> against a real SQL Server container, where
/// each task's deliverable ids are projected in SQL.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectTasksQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public GetProjectTasksQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
        MapsterConfiguration.Ensure();
    }

    [Fact]
    public async Task Handle_ReturnsTheDeliverablesEachTaskServes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        await using var context = _fixture.CreateContext();
        var handler = new GetProjectTasksQueryHandler(context);

        // Act
        var tasks = await handler.Handle(new GetProjectTasksQuery(plan.ProjectKey), ct);

        // Assert
        tasks.Single(t => t.Id == plan.DesignId).DeliverableIds.Should().BeEquivalentTo([plan.PortalId]);
        tasks.Single(t => t.Id == plan.BuildId).DeliverableIds.Should().BeEquivalentTo([plan.PortalId, plan.ReportsId]);
        tasks.Single(t => t.Id == plan.TestId).DeliverableIds.Should().BeEmpty();
    }
}
