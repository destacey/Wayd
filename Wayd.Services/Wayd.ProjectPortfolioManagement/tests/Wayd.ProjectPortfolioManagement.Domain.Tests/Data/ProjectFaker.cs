using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectFaker : PrivateConstructorFaker<Project>
{
    public ProjectFaker()
    {
        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => new ProjectKey(f.Random.AlphaNumeric(5)));
        RuleFor(x => x.Name, f => f.Commerce.ProductName());
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.Status, f => ProjectStatus.Proposed);
        RuleFor(x => x.DateRange, f => null); // Default is null for proposed projects.
        RuleFor(p => p.ExpenditureCategoryId, f => f.Random.Int(1, 10));
        RuleFor(x => x.PortfolioId, f => f.Random.Guid()); // Set by portfolio in real scenarios.
        RuleFor(x => x.ProgramId, f => null);
        RuleFor(x => x.Rank, f => f.Random.Double(1, 100000));
    }
}

public static class ProjectFakerExtensions
{
    public static ProjectFaker WithData(
        this ProjectFaker faker,
        Guid? id = null,
        ProjectKey? key = null,
        string? name = null,
        string? description = null,
        ProjectStatus? status = null,
        LocalDateRange? dateRange = null,
        int? expenditureCategoryId = null,
        Guid? portfolioId = null,
        Guid? programId = null,
        Dictionary<ProjectRole, HashSet<Guid>>? roles = null)
    {
        if (id.HasValue) { faker.RuleFor(x => x.Id, id.Value); }
        if (key is not null) { faker.RuleFor(x => x.Key, key); }
        if (!string.IsNullOrWhiteSpace(name)) { faker.RuleFor(x => x.Name, name); }
        if (!string.IsNullOrWhiteSpace(description)) { faker.RuleFor(x => x.Description, description); }
        if (status.HasValue) { faker.RuleFor(x => x.Status, status); }
        if (dateRange is not null) { faker.RuleFor(x => x.DateRange, dateRange); }
        if (expenditureCategoryId.HasValue) faker.RuleFor(p => p.ExpenditureCategoryId, expenditureCategoryId.Value);
        if (portfolioId.HasValue) { faker.RuleFor(x => x.PortfolioId, portfolioId.Value); }
        if (programId.HasValue) { faker.RuleFor(x => x.ProgramId, programId.Value); }

        if (roles is not null)
        {
            var projectId = id ?? Guid.NewGuid();
            if (!id.HasValue)
            {
                faker.RuleFor(x => x.Id, projectId);
            }

            HashSet<RoleAssignment<ProjectRole>> updatedRoles = [];
            foreach (var role in roles)
            {
                foreach (var employeeId in role.Value)
                {
                    updatedRoles.Add(new RoleAssignment<ProjectRole>(projectId, role.Key, employeeId));
                }
            }

            faker.RuleFor("_roles", x => updatedRoles);
        }

        return faker;
    }

    /// <summary>
    /// Generates a proposed project with a start date 10 days from now and an end date 5 months from now.
    /// </summary>
    /// <param name="faker"></param>
    /// <param name="dateTimeProvider"></param>
    /// <param name="portfolioId"></param>
    /// <param name="programId"></param>
    /// <returns></returns>
    public static Project AsProposed(this ProjectFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null, Guid? programId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(10);
        var endDate = startDate.PlusMonths(5);

        return faker.WithData(
            status: ProjectStatus.Proposed,
            dateRange: new LocalDateRange(startDate, endDate),
            portfolioId: portfolioId,
            programId: programId
        ).Generate();
    }

    /// <summary>
    /// Generates an approved project with a start date 10 days from now and an end date 5 months from now.
    /// </summary>
    public static Project AsApproved(this ProjectFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null, Guid? programId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(10);
        var endDate = startDate.PlusMonths(5);

        return faker.WithData(
            status: ProjectStatus.Approved,
            dateRange: new LocalDateRange(startDate, endDate),
            portfolioId: portfolioId,
            programId: programId
        ).Generate();
    }

    /// <summary>
    /// Generates an active project with a start date 10 days ago.
    /// </summary>
    public static Project AsActive(this ProjectFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null, Guid? programId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-10);
        var endDate = startDate.PlusMonths(5);

        return faker.WithData(
            status: ProjectStatus.Active,
            dateRange: new LocalDateRange(startDate, endDate),
            portfolioId: portfolioId,
            programId: programId
        ).Generate();
    }

    /// <summary>
    /// Generates a completed project with a start date 20 days ago and an end date 10 days ago.
    /// </summary>
    public static Project AsCompleted(this ProjectFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null, Guid? programId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-20);
        var endDate = startDate.PlusDays(10);

        return faker.WithData(
            status: ProjectStatus.Completed,
            dateRange: new LocalDateRange(startDate, endDate),
            portfolioId: portfolioId,
            programId: programId
        ).Generate();
    }

    /// <summary>
    /// Generates a cancelled project with a start date 15 days ago and an end date 5 days ago.
    /// </summary>
    public static Project AsCancelled(this ProjectFaker faker, TestingDateTimeProvider dateTimeProvider, Guid? portfolioId = null, Guid? programId = null)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-15);
        var endDate = startDate.PlusDays(5);

        return faker.WithData(
            status: ProjectStatus.Cancelled,
            dateRange: new LocalDateRange(startDate, endDate),
            portfolioId: portfolioId,
            programId: programId
        ).Generate();
    }

    /// <summary>
    /// Creates the specified number of tasks and adds them to the project's internal _tasks collection.
    /// This properly simulates EF Core's Include behavior for unit tests.
    /// </summary>
    /// <param name="project">The project to add tasks to.</param>
    /// <param name="taskCount">The number of tasks to create.</param>
    /// <param name="projectPhaseId">Optional phase ID to assign to all tasks.</param>
    /// <returns>The list of created tasks, also accessible via project.Tasks.</returns>
    public static List<ProjectTask> WithTasks(this Project project, int taskCount, Guid? projectPhaseId = null)
    {
        var tasks = new List<ProjectTask>();
        var taskFaker = new ProjectTaskFaker();

        for (int i = 1; i <= taskCount; i++)
        {
            var task = taskFaker.WithData(
                id: Guid.NewGuid(),
                projectId: project.Id,
                key: new ProjectTaskKey(project.Key, i),
                order: i,
                projectPhaseId: projectPhaseId
            ).Generate();

            tasks.Add(task);
            project.AddToPrivateList("_tasks", task);
            SetProjectNavigation(task, project);
        }

        return tasks;
    }

    /// <summary>
    /// Creates the specified number of tasks with custom configuration and adds them to the project's internal _tasks collection.
    /// This properly simulates EF Core's Include behavior for unit tests.
    /// </summary>
    /// <param name="project">The project to add tasks to.</param>
    /// <param name="taskCount">The number of tasks to create.</param>
    /// <param name="configureTask">An action to configure each task faker before generation. The int parameter is the task number (1-based).</param>
    /// <returns>The list of created tasks, also accessible via project.Tasks.</returns>
    public static List<ProjectTask> WithTasks(this Project project, int taskCount, Action<ProjectTaskFaker, int> configureTask)
    {
        var tasks = new List<ProjectTask>();

        for (int i = 1; i <= taskCount; i++)
        {
            var taskFaker = new ProjectTaskFaker()
                .WithData(
                    id: Guid.NewGuid(),
                    projectId: project.Id,
                    key: new ProjectTaskKey(project.Key, i),
                    order: i);

            configureTask(taskFaker, i);

            var task = taskFaker.Generate();
            tasks.Add(task);
            project.AddToPrivateList("_tasks", task);
            SetProjectNavigation(task, project);
        }

        return tasks;
    }

    private static void SetProjectNavigation(ProjectTask task, Project project)
    {
        typeof(ProjectTask)
            .GetProperty(nameof(ProjectTask.Project))!
            .SetValue(task, project);
    }

    /// <summary>
    /// Sets the project's fractional rank sort key at construction.
    /// </summary>
    public static ProjectFaker WithRank(this ProjectFaker faker, double rank)
    {
        faker.RuleFor(x => x.Rank, rank);
        return faker;
    }
}
