using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using Wayd.Tests.Shared.Extensions;
using TaskStatus = Wayd.ProjectPortfolioManagement.Domain.Enums.TaskStatus;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

public sealed class ProjectTaskFaker : PrivateConstructorFaker<ProjectTask>
{
    public ProjectTaskFaker()
    {
        var projectKey = new ProjectKey("TEST");
        var number = FakerHub.Random.Int(1, 10000);
        var taskKey = new ProjectTaskKey(projectKey, number);

        RuleFor(x => x.Id, f => f.Random.Guid());
        RuleFor(x => x.Key, f => taskKey);
        RuleFor(x => x.Number, f => number);
        RuleFor(x => x.ProjectId, f => f.Random.Guid());
        RuleFor(x => x.Name, f => f.Lorem.Sentence(3));
        RuleFor(x => x.Description, f => f.Lorem.Paragraph());
        RuleFor(x => x.Type, f => ProjectTaskType.Task);
        RuleFor(x => x.Status, f => TaskStatus.NotStarted);
        RuleFor(x => x.Priority, f => f.PickRandom(new TaskPriority[] { TaskPriority.Low, TaskPriority.Medium, TaskPriority.High, TaskPriority.High }));
        RuleFor(x => x.Progress, f => Progress.NotStarted());
        RuleFor(x => x.Order, f => f.Random.Int(1, 10));
        RuleFor(x => x.ParentId, f => null); // No parent by default
        RuleFor(x => x.ProjectPhaseId, f => Guid.Empty); // Default; set via WithProjectPhaseId for tests needing phases
        RuleFor(x => x.PlannedDateRange, f => null);
        RuleFor(x => x.EstimatedEffortHours, f => f.Random.Decimal(1, 100));
    }
}

public static class ProjectTaskFakerExtensions
{
    public static ProjectTaskFaker WithId(this ProjectTaskFaker faker, Guid? id)
    {
        faker.RuleFor(x => x.Id, id);

        return faker;
    }

    public static ProjectTaskFaker WithKey(this ProjectTaskFaker faker, ProjectTaskKey key)
    {
        faker.RuleFor(x => x.Key, key);
        faker.RuleFor(x => x.Number, key.TaskNumber);

        return faker;
    }

    public static ProjectTaskFaker WithProjectId(this ProjectTaskFaker faker, Guid? projectId)
    {
        faker.RuleFor(x => x.ProjectId, projectId);

        return faker;
    }

    public static ProjectTaskFaker WithName(this ProjectTaskFaker faker, string? name)
    {
        faker.RuleFor(x => x.Name, name);

        return faker;
    }

    public static ProjectTaskFaker WithDescription(this ProjectTaskFaker faker, string? description)
    {
        faker.RuleFor(x => x.Description, description);

        return faker;
    }

    public static ProjectTaskFaker WithType(this ProjectTaskFaker faker, ProjectTaskType? type)
    {
        faker.RuleFor(x => x.Type, type);

        return faker;
    }

    public static ProjectTaskFaker WithStatus(this ProjectTaskFaker faker, TaskStatus? status)
    {
        faker.RuleFor(x => x.Status, status);

        return faker;
    }

    public static ProjectTaskFaker WithPriority(this ProjectTaskFaker faker, TaskPriority? priority)
    {
        faker.RuleFor(x => x.Priority, priority);

        return faker;
    }

    public static ProjectTaskFaker WithProgress(this ProjectTaskFaker faker, Progress? progress)
    {
        faker.RuleFor(x => x.Progress, progress);

        return faker;
    }

    public static ProjectTaskFaker WithOrder(this ProjectTaskFaker faker, int? order)
    {
        faker.RuleFor(x => x.Order, order);

        return faker;
    }

    public static ProjectTaskFaker WithParentId(this ProjectTaskFaker faker, Guid? parentId)
    {
        faker.RuleFor(x => x.ParentId, parentId);

        return faker;
    }

    public static ProjectTaskFaker WithProjectPhaseId(this ProjectTaskFaker faker, Guid? projectPhaseId)
    {
        faker.RuleFor(x => x.ProjectPhaseId, projectPhaseId);

        return faker;
    }

    public static ProjectTaskFaker WithPlannedDateRange(this ProjectTaskFaker faker, FlexibleDateRange? plannedDateRange)
    {
        faker.RuleFor(x => x.PlannedDateRange, plannedDateRange);

        return faker;
    }

    public static ProjectTaskFaker WithPlannedDate(this ProjectTaskFaker faker, LocalDate? plannedDate)
    {
        faker.RuleFor(x => x.PlannedDate, plannedDate);

        return faker;
    }

    public static ProjectTaskFaker WithEstimatedEffortHours(this ProjectTaskFaker faker, decimal? estimatedEffortHours)
    {
        faker.RuleFor(x => x.EstimatedEffortHours, estimatedEffortHours);

        return faker;
    }

    /// <summary>
    /// Generates a task in NotStarted status with planned dates.
    /// </summary>
    public static ProjectTask AsNotStarted(this ProjectTaskFaker faker, TestingDateTimeProvider dateTimeProvider, Guid projectId, ProjectKey projectKey)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(5);
        var endDate = startDate.PlusDays(10);
        var progress = new Progress(Decimal.Zero);

        return faker.WithProjectId(projectId).WithKey(new ProjectTaskKey(projectKey, new Random().Next(1, 999))).WithStatus(TaskStatus.NotStarted).WithProgress(progress).WithPlannedDateRange(new FlexibleDateRange(startDate, endDate)).Generate();
    }

    /// <summary>
    /// Generates a task in InProgress status.
    /// </summary>
    public static ProjectTask AsInProgress(this ProjectTaskFaker faker, TestingDateTimeProvider dateTimeProvider, Guid projectId, ProjectKey projectKey)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-5);
        var endDate = now.PlusDays(10);
        var progress = new Progress(0.25m);

        return faker.WithProjectId(projectId).WithKey(new ProjectTaskKey(projectKey, new Random().Next(1, 999))).WithStatus(TaskStatus.InProgress).WithProgress(progress).WithPlannedDateRange(new FlexibleDateRange(startDate, endDate)).Generate();
    }

    /// <summary>
    /// Generates a task in Completed status.
    /// </summary>
    public static ProjectTask AsCompleted(this ProjectTaskFaker faker, TestingDateTimeProvider dateTimeProvider, Guid projectId, ProjectKey projectKey)
    {
        var now = dateTimeProvider.Today;
        var startDate = now.PlusDays(-15);
        var endDate = now.PlusDays(-5);
        var progress = Progress.Completed();

        return faker.WithProjectId(projectId).WithKey(new ProjectTaskKey(projectKey, new Random().Next(1, 999))).WithStatus(TaskStatus.Completed).WithProgress(progress).WithPlannedDateRange(new FlexibleDateRange(startDate, endDate)).Generate();
    }

    /// <summary>
    /// Generates a milestone task with a planned date.
    /// </summary>
    public static ProjectTask AsMilestone(this ProjectTaskFaker faker, TestingDateTimeProvider dateTimeProvider, Guid projectId, ProjectKey projectKey)
    {
        var now = dateTimeProvider.Today;
        var milestoneDate = now.PlusDays(30);

        return faker.WithProjectId(projectId).WithKey(new ProjectTaskKey(projectKey, new Random().Next(1, 999))).WithType(ProjectTaskType.Milestone).WithPlannedDate(milestoneDate).Generate();
    }

    /// <summary>
    /// Adds assignee role assignments to a generated task.
    /// </summary>
    /// <param name="task">The task to add assignees to.</param>
    /// <param name="employeeIds">The employee IDs to assign as task assignees.</param>
    /// <returns>The task with assignees added.</returns>
    public static ProjectTask WithAssignees(this ProjectTask task, params Guid[] employeeIds)
    {
        var roles = new HashSet<RoleAssignment<TaskRole>>();
        foreach (var employeeId in employeeIds)
        {
            roles.Add(new RoleAssignment<TaskRole>(task.Id, TaskRole.Assignee, employeeId));
        }

        task.SetPrivateField("_roles", roles);
        return task;
    }
}