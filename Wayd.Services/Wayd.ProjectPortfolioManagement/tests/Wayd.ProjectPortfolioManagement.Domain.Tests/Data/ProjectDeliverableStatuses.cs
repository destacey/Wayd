using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

/// <summary>
/// Statuses of one deliverable workflow, as the application layer would resolve them, so domain tests can move
/// deliverables without the engine. Any move is allowed, as in the seeded default.
/// </summary>
public sealed class ProjectDeliverableStatuses
{
    public Guid WorkflowId { get; } = Guid.NewGuid();

    public StatusRef NotStarted { get; }
    public StatusRef InProgress { get; }
    public StatusRef InReview { get; }
    public StatusRef Accepted { get; }
    public StatusRef Descoped { get; }

    public ProjectDeliverableStatuses()
    {
        NotStarted = Status("Not Started", StatusCategory.Proposed, ProjectDeliverableStatusAlias.NotStarted);
        InProgress = Status("In Progress", StatusCategory.Active, ProjectDeliverableStatusAlias.InProgress);
        InReview = Status("In Review", StatusCategory.Active, ProjectDeliverableStatusAlias.None);
        Accepted = Status("Accepted", StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted);
        Descoped = Status("Descoped", StatusCategory.Removed, ProjectDeliverableStatusAlias.Descoped);
    }

    private StatusRef Status(string name, StatusCategory category, ProjectDeliverableStatusAlias alias) =>
        new(WorkflowId, Guid.NewGuid(), name, category, (int)alias);
}
