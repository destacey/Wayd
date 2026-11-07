using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="UpdateProjectTaskCommandHandler"/> against a real SQL Server container.
/// Setting a task's deliverables loads only that task's links and the named deliverables; a filter that loaded
/// too little would silently keep or drop links, which the in-memory fake cannot show.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class UpdateProjectTaskCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public UpdateProjectTaskCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static UpdateProjectTaskCommand Unchanged(Domain.Models.ProjectTask task, List<Guid>? deliverableIds) =>
        new(task.Id, task.Name, task.Description, task.Status, task.Priority, task.Progress,
            task.ParentId ?? task.ProjectStageId, task.PlannedDateRange, task.PlannedDate, task.EstimatedEffortHours, null, deliverableIds);

    private async Task<List<(Guid DeliverableId, Guid TaskId)>> Links(CancellationToken ct)
    {
        await using var verify = _fixture.CreateContext();
        var links = await verify.ProjectDeliverableTasks
            .Select(l => new { l.ProjectDeliverableId, l.ProjectTaskId })
            .ToListAsync(ct);
        return [.. links.Select(l => (l.ProjectDeliverableId, l.ProjectTaskId))];
    }

    [Fact]
    public async Task Handle_WithDeliverableIds_ReplacesThatTasksLinksAlone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        await using var context = _fixture.CreateContext();
        var build = await context.ProjectTasks.AsNoTracking().SingleAsync(t => t.Id == plan.BuildId, ct);
        var handler = new UpdateProjectTaskCommandHandler(context, NullLogger<UpdateProjectTaskCommandHandler>.Instance, ActiveProjectWithDeliverable.Clock());

        // Act
        var result = await handler.Handle(Unchanged(build, [plan.ReportsId]), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (await Links(ct)).Should().BeEquivalentTo(new[]
        {
            (plan.PortalId, plan.DesignId),
            (plan.ReportsId, plan.BuildId),
        });
    }

    [Fact]
    public async Task Handle_WithoutDeliverableIds_KeepsTheTasksLinks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await ProposedProjectWithLinkedPlan.Seed(_fixture, ct);
        await using var context = _fixture.CreateContext();
        var build = await context.ProjectTasks.AsNoTracking().SingleAsync(t => t.Id == plan.BuildId, ct);
        var handler = new UpdateProjectTaskCommandHandler(context, NullLogger<UpdateProjectTaskCommandHandler>.Instance, ActiveProjectWithDeliverable.Clock());

        // Act
        var result = await handler.Handle(Unchanged(build, null), ct);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        (await Links(ct)).Should().HaveCount(3);
    }
}
