using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// <see cref="GetMyProjectTasksQueryHandler"/> and <see cref="GetMyProjectDeliverablesQueryHandler"/> against a real
/// SQL Server container: both filter through navigations and project converted keys, which only a real provider
/// proves translate. Ordering and filtering rules are covered by the unit tests.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class MyProjectWorkQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public MyProjectWorkQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
        MapsterConfiguration.Ensure();
    }

    private async Task<(Guid EmployeeId, Guid TaskId, Guid MilestoneId, Guid DeliverableId)> Seed(CancellationToken cancellationToken)
    {
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, cancellationToken, activateTheProject: false);
        await using var context = _fixture.CreateContext();

        var lifecycle = ProjectLifecycle.Create("Standard", "Standard delivery lifecycle", [("Delivery", "Delivery stage")]);
        lifecycle.Activate();
        await context.ProjectLifecycles.AddAsync(lifecycle, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var project = await context.Projects
            .Include(p => p.Deliverables)
            .SingleAsync(p => p.Id == seed.ProjectId, cancellationToken);
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, SqlServerDbContextFixture.FixedNow)
            .IsSuccess.Should().BeTrue();
        var stageId = project.Stages.Single().Id;
        var today = SqlServerDbContextFixture.FixedNow.InUtc().Date;
        var assigned = new Dictionary<TaskRole, HashSet<Guid>> { [TaskRole.Assignee] = [seed.EmployeeId] };

        var task = project.CreateTask(1, "Design", null, ProjectTaskType.Task, TaskStatus.InProgress, TaskPriority.High, null, stageId,
            new FlexibleDateRange(today.PlusDays(-5), today.PlusDays(-1)), null, null, assigned).Value;
        var milestone = project.CreateTask(2, "Launch", null, ProjectTaskType.Milestone, TaskStatus.NotStarted, TaskPriority.Medium, null, stageId,
            null, today.PlusDays(10), null, assigned).Value;
        project.CreateTask(3, "Unassigned", null, ProjectTaskType.Task, TaskStatus.NotStarted, TaskPriority.Low, null, stageId,
            new FlexibleDateRange(today, today.PlusDays(2)), null, null, null).Value.Should().NotBeNull();

        project.UpdateDeliverable(PpmActor.System, ProjectAncestryRoles.None, seed.DeliverableId, "Customer portal", null, null,
            seed.EmployeeId, today.PlusDays(-3), SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        project.Activate(PpmActor.System, ProjectAncestryRoles.None, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(cancellationToken);

        return (seed.EmployeeId, task.Id, milestone.Id, seed.DeliverableId);
    }

    private static ICurrentPrincipal PrincipalFor(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal.Object;
    }

    [Fact]
    public async Task GetMyProjectTasks_ReadsTheCallersOpenTasksFromSqlServer()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (employeeId, taskId, milestoneId, _) = await Seed(cancellationToken);
        await using var context = _fixture.CreateContext();
        var handler = new GetMyProjectTasksQueryHandler(context, PrincipalFor(employeeId));

        // Act
        var result = await handler.Handle(new GetMyProjectTasksQuery([ProjectStatus.Approved, ProjectStatus.Active]), cancellationToken);

        // Assert
        var today = SqlServerDbContextFixture.FixedNow.InUtc().Date;
        result.Select(t => t.Id).Should().Equal(taskId, milestoneId);
        result[0].Key.Should().Be("GEMINI-1");
        result[0].DueDate.Should().Be(today.PlusDays(-1));
        result[0].Project.Key.Should().Be("GEMINI");
        result[0].Project.Name.Should().Be("Gemini");
        result[1].DueDate.Should().Be(today.PlusDays(10));
    }

    [Fact]
    public async Task GetMyProjectDeliverables_ReadsTheCallersOpenDeliverablesFromSqlServer()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (employeeId, _, _, deliverableId) = await Seed(cancellationToken);
        await using var context = _fixture.CreateContext();
        var handler = new GetMyProjectDeliverablesQueryHandler(context, PrincipalFor(employeeId));

        // Act
        var result = await handler.Handle(new GetMyProjectDeliverablesQuery(), cancellationToken);

        // Assert
        var deliverable = result.Should().ContainSingle().Which;
        deliverable.Id.Should().Be(deliverableId);
        deliverable.TargetDate.Should().Be(SqlServerDbContextFixture.FixedNow.InUtc().Date.PlusDays(-3));
        deliverable.Status.Name.Should().Be("Not Started");
        deliverable.Project.Key.Should().Be("GEMINI");
    }
}
