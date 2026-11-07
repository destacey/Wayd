using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain;

/// <summary>
/// The kinds of PPM record the status workflow engine governs.
/// </summary>
/// <remarks>
/// Declared here rather than in the engine so Common carries no PPM vocabulary. The key is persisted on
/// every workflow row and status transition, and the alias values on every record.
/// <strong>Never change either once shipped</strong>: renaming the key orphans its workflows, and
/// renumbering an alias silently changes what existing rows mean.
/// </remarks>
public static class PpmWorkflowOwners
{
    /// <summary>A project deliverable. Governs it from commitment to acceptance.</summary>
    /// <remarks>
    /// Organization-wide assignment only: every project's deliverables follow the organization's workflow.
    /// <para>
    /// A required alias is one something resolves or checks: a deliverable starts in Not Started, an intake
    /// request is met when its deliverables are Accepted, and Descoped is how a project closes one without
    /// delivering it. In Progress is optional, so a company can replace it with its own Active stages. Every
    /// other rule is written against the category: a project cannot complete while a deliverable is Proposed
    /// or Active, and any Removed status needs a reason.
    /// </para>
    /// <para>
    /// No restricted transitions in v1: reopening must reach an open status from Accepted or Descoped, which
    /// a restricted workflow could forbid.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor ProjectDeliverable = new(
        ProjectDeliverableWorkflow.OwnerType,
        "Project Deliverable",
        new Dictionary<int, WorkflowAlias>
        {
            [(int)ProjectDeliverableStatusAlias.NotStarted] = new("Not Started", StatusCategory.Proposed),
            [(int)ProjectDeliverableStatusAlias.InProgress] = new("In Progress", StatusCategory.Active),
            [(int)ProjectDeliverableStatusAlias.Accepted] = new("Accepted", StatusCategory.Done),
            [(int)ProjectDeliverableStatusAlias.Descoped] = new("Descoped", StatusCategory.Removed),
        },
        [
            (int)ProjectDeliverableStatusAlias.NotStarted,
            (int)ProjectDeliverableStatusAlias.Accepted,
            (int)ProjectDeliverableStatusAlias.Descoped,
        ],
        [StatusCategory.Proposed, StatusCategory.Done, StatusCategory.Removed]);

    /// <summary>Every owner type this module contributes, for registration at startup.</summary>
    public static WorkflowOwnerDescriptor[] All => [ProjectDeliverable];

    /// <summary>
    /// Registers this module's owner types with the engine. Call once during startup, before anything
    /// resolves a workflow.
    /// </summary>
    public static void Register() => WorkflowOwners.Register(All);
}
