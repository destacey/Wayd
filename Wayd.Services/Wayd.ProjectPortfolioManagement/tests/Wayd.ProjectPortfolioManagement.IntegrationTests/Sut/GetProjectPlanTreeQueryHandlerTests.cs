using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="GetProjectPlanTreeQueryHandler"/> against a real SQL Server container, where
/// the plan's tasks load with their deliverable links.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectPlanTreeQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public GetProjectPlanTreeQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_GivesEachTaskNodeTheDeliverablesItServes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        await using var context = _fixture.CreateContext();
        var handler = new GetProjectPlanTreeQueryHandler(context);

        // Act
        var stages = await handler.Handle(new GetProjectPlanTreeQuery(new ProjectIdOrKey(plan.ProjectKey)), ct);

        // Assert
        var stage = stages.Should().ContainSingle().Subject;
        stage.DeliverableIds.Should().BeEmpty();
        stage.Children.Single(t => t.Id == plan.BuildId).DeliverableIds.Should().BeEquivalentTo([plan.PortalId, plan.ReportsId]);
        stage.Children.Single(t => t.Id == plan.TestId).DeliverableIds.Should().BeEmpty();
    }
}
