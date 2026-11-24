using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

/// <summary>
/// A Proposed project in an active portfolio, following a one-stage lifecycle so tasks can be created under the
/// stage.
/// </summary>
/// <param name="ProjectId">The project.</param>
/// <param name="StageId">The project's one stage, the parent of its top-level tasks.</param>
public sealed record ProposedProjectWithStage(Guid ProjectId, Guid StageId)
{
    /// <summary>Resets the PPM data and seeds the project.</summary>
    public static async Task<ProposedProjectWithStage> Seed(SqlServerDbContextFixture fixture, CancellationToken cancellationToken)
    {
        await fixture.ResetPpmData(cancellationToken);
        await using var context = fixture.CreateContext();

        var category = ExpenditureCategory.Create("Capital", "Capital spend", isCapitalizable: true, requiresDepreciation: true);
        await context.ExpenditureCategories.AddAsync(category, cancellationToken);

        var lifecycle = ProjectLifecycle.Create("Standard", "Standard delivery lifecycle", [("Delivery", "Delivery stage")]);
        lifecycle.Activate();
        await context.ProjectLifecycles.AddAsync(lifecycle, cancellationToken);

        var portfolio = ProjectPortfolio.Create(
            "Delivery", "Delivery portfolio", null, EventActor.System, SqlServerDbContextFixture.FixedNow);
        await context.Portfolios.AddAsync(portfolio, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        portfolio.Activate(PpmActor.System, SqlServerDbContextFixture.FixedNow.InUtc().Date, SqlServerDbContextFixture.FixedNow);
        var project = portfolio.CreateProject(
            "Gemini",
            "Gemini description",
            new ProjectKey("GEMINI"),
            category.Id,
            dateRange: null,
            programId: null,
            businessCase: null,
            expectedBenefits: null,
            roles: null,
            strategicThemes: null,
            SqlServerDbContextFixture.FixedNow,
            PpmActor.System).Value;
        project.AssignLifecycle(PpmActor.System, ProjectAncestryRoles.None, lifecycle, SqlServerDbContextFixture.FixedNow);
        await context.SaveChangesAsync(cancellationToken);

        return new ProposedProjectWithStage(project.Id, project.Stages.Single().Id);
    }

    /// <summary>Creates a not-started task on <paramref name="project"/>, failing the test if the project refuses it.</summary>
    public static ProjectTask CreateTask(Project project, int number, string name, Guid parentId, FlexibleDateRange? dates = null)
    {
        var result = project.CreateTask(
            nextNumber: number,
            name: name,
            description: null,
            type: ProjectTaskType.Task,
            status: TaskStatus.NotStarted,
            priority: TaskPriority.Medium,
            progress: null,
            parentId: parentId,
            plannedDateRange: dates,
            plannedDate: null,
            estimatedEffortHours: null,
            roles: null);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        return result.Value;
    }
}
