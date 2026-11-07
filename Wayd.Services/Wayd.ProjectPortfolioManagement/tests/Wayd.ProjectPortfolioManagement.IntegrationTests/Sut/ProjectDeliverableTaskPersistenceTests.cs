using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Persistence tests for <see cref="ProjectDeliverableTask"/>, the links between plan tasks and deliverables,
/// against a real SQL Server container.
/// <para>
/// Setting links reads the loaded links as the whole set, and the database cascades a link only from its task.
/// A missing <c>.Include</c> and a missing cascade are both invisible to the in-memory fake.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ProjectDeliverableTaskPersistenceTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ProjectDeliverableTaskPersistenceTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record Plan(Guid ProjectId, Guid PortalId, Guid ReportsId, Guid DesignId, Guid BuildId, Guid TestId);

    /// <summary>
    /// The seeded project with a second deliverable and three tasks, where Design and Build serve the portal and
    /// Build also serves the reports. The project is left Proposed, so its deliverables can still be deleted.
    /// </summary>
    private async Task<Plan> SeedPlan(CancellationToken ct)
    {
        var seed = await ActiveProjectWithDeliverable.Seed(_fixture, acceptTheDeliverable: false, ct, activateTheProject: false);
        await using var context = _fixture.CreateContext();

        var lifecycle = ProjectLifecycle.Create("Standard", "Standard delivery lifecycle", [("Delivery", "Delivery stage")]);
        lifecycle.Activate();
        await context.ProjectLifecycles.AddAsync(lifecycle, ct);
        await context.SaveChangesAsync(ct);

        var project = await context.Projects
            .Include(p => p.Deliverables)
            .SingleAsync(p => p.Id == seed.ProjectId, ct);
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, SqlServerDbContextFixture.FixedNow)
            .IsSuccess.Should().BeTrue();
        var stageId = project.Stages.Single().Id;

        var reports = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, "Reports", null, null, null, null,
            seed.Statuses[ProjectDeliverableStatusAlias.NotStarted], SqlServerDbContextFixture.FixedNow).Value;

        ProjectTask Create(int number, string name) => project.CreateTask(number, name, null, ProjectTaskType.Task,
            Domain.Enums.TaskStatus.NotStarted, TaskPriority.Medium, null, stageId, null, null, null, null).Value;
        var design = Create(1, "Design");
        var build = Create(2, "Build");
        var test = Create(3, "Test");

        project.SetDeliverableTasks(seed.DeliverableId, [design.Id, build.Id]).IsSuccess.Should().BeTrue();
        project.SetDeliverableTasks(reports.Id, [build.Id]).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        return new Plan(seed.ProjectId, seed.DeliverableId, reports.Id, design.Id, build.Id, test.Id);
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
    public async Task SetDeliverableTasks_LoadingOnlyThatDeliverableAndTheNamedTasks_ReplacesItsLinksAlone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await SeedPlan(ct);
        await using var context = _fixture.CreateContext();
        var named = new[] { plan.BuildId, plan.TestId };
        var project = await context.Projects
            .Include(p => p.Deliverables.Where(d => d.Id == plan.PortalId))
                .ThenInclude(d => d.TaskLinks)
            .Include(p => p.Tasks.Where(t => named.Contains(t.Id)))
            .SingleAsync(p => p.Id == plan.ProjectId, ct);

        // Act
        var result = project.SetDeliverableTasks(plan.PortalId, named);
        await context.SaveChangesAsync(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Tasks.Should().HaveCount(2, "only the named tasks are loaded, never the whole plan");
        (await Links(ct)).Should().BeEquivalentTo(new[]
        {
            (plan.PortalId, plan.BuildId),
            (plan.PortalId, plan.TestId),
            (plan.ReportsId, plan.BuildId),
        });
    }

    [Fact]
    public async Task SetTaskDeliverables_LoadingOnlyThatTaskAndTheNamedDeliverables_ReplacesItsLinksAlone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await SeedPlan(ct);
        await using var context = _fixture.CreateContext();
        var named = new[] { plan.ReportsId };
        var project = await context.Projects
            .Include(p => p.Tasks.Where(t => t.Id == plan.BuildId))
                .ThenInclude(t => t.DeliverableLinks)
            .Include(p => p.Deliverables.Where(d => named.Contains(d.Id)))
            .SingleAsync(p => p.Id == plan.ProjectId, ct);

        // Act
        var result = project.SetTaskDeliverables(plan.BuildId, named);
        await context.SaveChangesAsync(ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (await Links(ct)).Should().BeEquivalentTo(new[]
        {
            (plan.PortalId, plan.DesignId),
            (plan.ReportsId, plan.BuildId),
        });
    }

    [Fact]
    public async Task DeleteTask_WithItsLinksNotLoaded_CascadesThemInTheDatabase()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await SeedPlan(ct);
        await using var context = _fixture.CreateContext();
        var project = await context.Projects
            .Include(p => p.Tasks.Where(t => t.Id == plan.DesignId))
            .SingleAsync(p => p.Id == plan.ProjectId, ct);

        // Act
        project.DeleteTask(plan.DesignId).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Assert
        (await Links(ct)).Should().BeEquivalentTo(new[]
        {
            (plan.PortalId, plan.BuildId),
            (plan.ReportsId, plan.BuildId),
        });
    }

    [Fact]
    public async Task RemoveDeliverable_WithItsLinksLoaded_DeletesThemWithIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await SeedPlan(ct);
        await using var context = _fixture.CreateContext();
        var project = await context.Projects
            .Include(p => p.Deliverables.Where(d => d.Id == plan.ReportsId))
                .ThenInclude(d => d.TaskLinks)
            .SingleAsync(p => p.Id == plan.ProjectId, ct);

        // Act
        project.RemoveDeliverable(PpmActor.System, ProjectAncestryRoles.None, plan.ReportsId, SqlServerDbContextFixture.FixedNow)
            .IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ProjectDeliverables.AnyAsync(d => d.Id == plan.ReportsId, ct)).Should().BeFalse();
        (await Links(ct)).Should().NotContain(l => l.DeliverableId == plan.ReportsId);
        (await Links(ct)).Should().HaveCount(2);
    }

    [Fact]
    public async Task RemoveDeliverable_WithItsLinksNotLoaded_IsRefusedByTheDatabase()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await SeedPlan(ct);
        await using var context = _fixture.CreateContext();
        var project = await context.Projects
            .Include(p => p.Deliverables.Where(d => d.Id == plan.ReportsId))
            .SingleAsync(p => p.Id == plan.ProjectId, ct);
        project.RemoveDeliverable(PpmActor.System, ProjectAncestryRoles.None, plan.ReportsId, SqlServerDbContextFixture.FixedNow)
            .IsSuccess.Should().BeTrue();

        // Act
        var act = () => context.SaveChangesAsync(ct);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>("the database does not cascade from a deliverable to its links");
    }

    [Fact]
    public async Task DeletingTheProject_RemovesItsDeliverablesTasksAndLinks()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var plan = await SeedPlan(ct);
        await using var context = _fixture.CreateContext();
        var project = await context.Projects.SingleAsync(p => p.Id == plan.ProjectId, ct);

        // Act
        context.Projects.Remove(project);
        await context.SaveChangesAsync(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        (await verify.ProjectDeliverables.AnyAsync(d => d.ProjectId == plan.ProjectId, ct)).Should().BeFalse();
        (await verify.ProjectTasks.AnyAsync(t => t.ProjectId == plan.ProjectId, ct)).Should().BeFalse();
        (await Links(ct)).Should().BeEmpty();
    }
}
