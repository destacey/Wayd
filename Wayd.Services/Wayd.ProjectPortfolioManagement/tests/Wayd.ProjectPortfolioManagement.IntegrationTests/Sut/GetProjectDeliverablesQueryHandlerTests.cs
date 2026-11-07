using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="GetProjectDeliverablesQueryHandler"/> and
/// <see cref="GetProjectDeliverableQueryHandler"/> against a real SQL Server container. The link count is
/// projected in SQL and progress reads the plan's owned date ranges, neither of which the in-memory fake can
/// prove translates.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetProjectDeliverablesQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public GetProjectDeliverablesQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
        MapsterConfiguration.Ensure();
    }

    [Fact]
    public async Task Handle_DerivesEachDeliverablesProgressFromItsLinkedTasks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        await using var context = _fixture.CreateContext();
        var handler = new GetProjectDeliverablesQueryHandler(context);

        // Act
        var deliverables = await handler.Handle(new GetProjectDeliverablesQuery(new ProjectIdOrKey(plan.ProjectKey)), ct);

        // Assert
        deliverables.Should().NotBeNull();
        var portal = deliverables!.Single(d => d.Id == plan.PortalId);
        portal.LinkedTaskCount.Should().Be(2);
        portal.Progress.Should().Be(50m);
        var reports = deliverables!.Single(d => d.Id == plan.ReportsId);
        reports.LinkedTaskCount.Should().Be(1);
        reports.Progress.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_ForADeliverableNothingServes_ReportsNoProgress()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        await using (var unlink = _fixture.CreateContext())
        {
            var unlinked = await new SetProjectDeliverableTasksCommandHandler(unlink)
                .Handle(new SetProjectDeliverableTasksCommand(plan.ProjectId, plan.ReportsId, []), ct);
            unlinked.IsSuccess.Should().BeTrue();
        }
        await using var context = _fixture.CreateContext();
        var handler = new GetProjectDeliverableQueryHandler(context);

        // Act
        var reports = await handler.Handle(new GetProjectDeliverableQuery(new ProjectIdOrKey(plan.ProjectId.ToString()), plan.ReportsId), ct);

        // Assert
        reports.Should().NotBeNull();
        reports!.LinkedTaskCount.Should().Be(0);
        reports.Progress.Should().BeNull("a deliverable nothing in the plan serves has no progress, not 0%");
    }
}
