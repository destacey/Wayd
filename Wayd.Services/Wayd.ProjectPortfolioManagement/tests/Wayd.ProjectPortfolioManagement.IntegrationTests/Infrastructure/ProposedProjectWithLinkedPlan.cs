using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

/// <summary>
/// A Proposed project with two deliverables and three undated plan tasks, seeded through EF exactly as
/// production writes it. Design (0%) and Build (100%) serve the portal, Build also serves the reports, and Test
/// serves nothing. Left Proposed, so its deliverables can still be deleted.
/// </summary>
public sealed record ProposedProjectWithLinkedPlan(
    Guid ProjectId,
    string ProjectKey,
    Guid EmployeeId,
    Guid PortalId,
    Guid ReportsId,
    Guid DesignId,
    Guid BuildId,
    Guid TestId)
{
    /// <summary>Resets the PPM data and seeds the project and its linked plan.</summary>
    /// <param name="employeeOwnsThePortfolio">Makes the employee lead the project through its portfolio.</param>
    public static async Task<ProposedProjectWithLinkedPlan> Seed(
        SqlServerDbContextFixture fixture, CancellationToken cancellationToken, bool employeeOwnsThePortfolio = false)
    {
        var seed = await ActiveProjectWithDeliverable.Seed(fixture, acceptTheDeliverable: false, cancellationToken,
            employeeOwnsThePortfolio: employeeOwnsThePortfolio, activateTheProject: false);
        await using var context = fixture.CreateContext();

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

        var reports = project.AddDeliverable(PpmActor.System, ProjectAncestryRoles.None, "Reports", null, null, null, null,
            seed.Statuses[ProjectDeliverableStatusAlias.NotStarted], SqlServerDbContextFixture.FixedNow).Value;

        ProjectTask Create(int number, string name, decimal progress) => project.CreateTask(number, name, null, ProjectTaskType.Task,
            Domain.Enums.TaskStatus.NotStarted, TaskPriority.Medium, new Progress(progress), stageId, null, null, null, null).Value;
        var design = Create(1, "Design", 0m);
        var build = Create(2, "Build", 100m);
        var test = Create(3, "Test", 0m);

        project.SetDeliverableTasks(seed.DeliverableId, [design.Id, build.Id]).IsSuccess.Should().BeTrue();
        project.SetDeliverableTasks(reports.Id, [build.Id]).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(cancellationToken);

        return new ProposedProjectWithLinkedPlan(seed.ProjectId, project.Key.Value, seed.EmployeeId, seed.DeliverableId, reports.Id, design.Id, build.Id, test.Id);
    }
}
