namespace Wayd.ProjectPortfolioManagement.Domain.Models;

/// <summary>
/// Records that a plan task serves a deliverable of the same project. Part of the <see cref="Project"/>
/// aggregate: a task may serve several deliverables and a deliverable may be served by several tasks.
/// </summary>
/// <remarks>
/// Links are plan work, so they raise no events and do not advance <see cref="Project.Revision"/>. Created and
/// removed only through <see cref="Project.SetDeliverableTasks"/> and <see cref="Project.SetTaskDeliverables"/>.
/// </remarks>
public sealed class ProjectDeliverableTask : ISystemAuditable
{
    private ProjectDeliverableTask() { }

    private ProjectDeliverableTask(Guid projectDeliverableId, Guid projectTaskId)
    {
        ProjectDeliverableId = projectDeliverableId;
        ProjectTaskId = projectTaskId;
    }

    /// <summary>The deliverable the task serves.</summary>
    public Guid ProjectDeliverableId { get; private init; }

    /// <summary>The linked deliverable.</summary>
    public ProjectDeliverable? ProjectDeliverable { get; private set; }

    /// <summary>The task that serves the deliverable.</summary>
    public Guid ProjectTaskId { get; private init; }

    /// <summary>The linked task.</summary>
    public ProjectTask? ProjectTask { get; private set; }

    /// <summary>Links a task to a deliverable. The project checks both belong to it.</summary>
    internal static ProjectDeliverableTask Create(Guid projectDeliverableId, Guid projectTaskId) =>
        new(projectDeliverableId, projectTaskId);
}
