using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain;

/// <summary>
/// The kinds of Demand Management record the status workflow engine governs.
/// </summary>
/// <remarks>
/// Declared here rather than in the engine so Common carries no Demand Management vocabulary. The key is
/// persisted on every workflow row and status transition, and the alias values on every request.
/// <strong>Never change either once shipped</strong>: renaming the key orphans its workflows, and
/// renumbering an alias silently changes what existing rows mean.
/// </remarks>
public static class DemandManagementWorkflowOwners
{
    /// <summary>An intake request. Governs its review, from submission to a decision.</summary>
    /// <remarks>
    /// A desk is the assignment scope, so each desk follows its own workflow or the organization's default.
    /// <para>
    /// A required alias is one something resolves or checks: a request starts in Submitted, asking the
    /// requester a question moves it to Waiting on Requester, withdrawing moves it to Withdrawn, and
    /// marking a duplicate needs Duplicate. Accepted and Fulfilled are optional, kept for approvals and
    /// automatic fulfilment. Every other rule a request follows is written against its category, so a
    /// company's own statuses keep them without a meaning: any Removed status but Withdrawn, and any Done
    /// status, needs a reason.
    /// </para>
    /// <para>
    /// All four categories are required: a request must be able to wait for review, be taken on, be
    /// finished, and be closed without being done.
    /// </para>
    /// <para>
    /// No restricted transitions: withdrawing must reach Withdrawn from any open status, which a
    /// restricted workflow could forbid.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor IntakeRequest = new(
        "demand.intake-request",
        "Intake Request",
        new Dictionary<int, WorkflowAlias>
        {
            [(int)IntakeRequestStatusAlias.Submitted] = new("Submitted", StatusCategory.Proposed),
            [(int)IntakeRequestStatusAlias.WaitingOnRequester] = new("Waiting on Requester", StatusCategory.Proposed),
            [(int)IntakeRequestStatusAlias.Accepted] = new("Accepted", StatusCategory.Active),
            [(int)IntakeRequestStatusAlias.Fulfilled] = new("Fulfilled", StatusCategory.Done),
            [(int)IntakeRequestStatusAlias.Duplicate] = new("Duplicate", StatusCategory.Removed),
            [(int)IntakeRequestStatusAlias.Withdrawn] = new("Withdrawn", StatusCategory.Removed),
        },
        [
            (int)IntakeRequestStatusAlias.Submitted,
            (int)IntakeRequestStatusAlias.WaitingOnRequester,
            (int)IntakeRequestStatusAlias.Duplicate,
            (int)IntakeRequestStatusAlias.Withdrawn,
        ],
        [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done, StatusCategory.Removed],
        featureFlag: FeatureFlags.Names.DemandManagement);

    /// <summary>Every owner type this module contributes, for registration at startup.</summary>
    public static WorkflowOwnerDescriptor[] All => [IntakeRequest];

    /// <summary>
    /// Registers this module's owner types with the engine. Call once during startup, before anything
    /// resolves a workflow.
    /// </summary>
    public static void Register() => WorkflowOwners.Register(All);
}
