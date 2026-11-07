using Microsoft.EntityFrameworkCore;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="SetProjectDeliverableTasksCommandHandler"/> against a real SQL Server
/// container. The handler loads that deliverable with its links and only the named tasks; a filter that loaded too
/// little would silently keep or drop links, which the in-memory fake cannot show.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class SetProjectDeliverableTasksCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public SetProjectDeliverableTasksCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<List<(Guid DeliverableId, Guid TaskId)>> Links(CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        var links = await verify.ProjectDeliverableTasks
            .Select(l => new { l.ProjectDeliverableId, l.ProjectTaskId })
            .ToListAsync(ct);
        return [.. links.Select(l => (l.ProjectDeliverableId, l.ProjectTaskId))];
    }

    [Fact]
    public async Task Handle_ReplacesThatDeliverablesLinksAlone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new SetProjectDeliverableTasksCommandHandler(context)
                .Handle(new SetProjectDeliverableTasksCommand(plan.ProjectId, plan.PortalId, [plan.BuildId, plan.TestId]), ct);
        }

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (await Links(ct)).Should().BeEquivalentTo(new[]
        {
            (plan.PortalId, plan.BuildId),
            (plan.PortalId, plan.TestId),
            (plan.ReportsId, plan.BuildId),
        });
    }

    [Fact]
    public async Task Handle_WithATaskOfAnotherProject_ChangesNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        var unknownTaskId = Guid.NewGuid();

        // Act
        CSharpFunctionalExtensions.Result result;
        await using (var context = _fixture.CreateContext())
        {
            result = await new SetProjectDeliverableTasksCommandHandler(context)
                .Handle(new SetProjectDeliverableTasksCommand(plan.ProjectId, plan.PortalId, [unknownTaskId]), ct);
        }

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(unknownTaskId.ToString());
        (await Links(ct)).Should().HaveCount(3);
    }
}
