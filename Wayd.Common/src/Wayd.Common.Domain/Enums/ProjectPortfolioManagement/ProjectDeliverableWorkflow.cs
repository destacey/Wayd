namespace Wayd.Common.Domain.Enums.ProjectPortfolioManagement;

/// <summary>
/// What another module needs to recognise a project deliverable in the status workflow engine's shared events.
/// </summary>
public static class ProjectDeliverableWorkflow
{
    /// <summary>
    /// The owner type the engine knows project deliverables by, as <c>StatusWorkflowSwitchedEvent.OwnerType</c>
    /// carries it. Persisted on every workflow and transition row, so it never changes.
    /// </summary>
    public const string OwnerType = "ppm.project-deliverable";
}
