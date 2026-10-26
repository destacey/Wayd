using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows;

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
    /// Every alias but Fulfilled is required, because something resolves or checks each one: a request
    /// starts in Submitted, asking the requester a question moves it to Waiting on Requester, withdrawing
    /// moves it to Withdrawn, and Accepted, Deferred, Declined and Duplicate carry rules of their own.
    /// Fulfilled is optional because the rule a finished request follows is written against the Done
    /// category, so a company's own finishing status keeps it.
    /// </para>
    /// <para>
    /// No restricted transitions: withdrawing must reach Withdrawn from any open status, which a
    /// restricted workflow could forbid.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor IntakeRequest = new(
        "demand.intake-request",
        "Intake Request",
        new Dictionary<int, string>
        {
            [(int)IntakeRequestStatusAlias.Submitted] = "Submitted",
            [(int)IntakeRequestStatusAlias.WaitingOnRequester] = "Waiting on Requester",
            [(int)IntakeRequestStatusAlias.Accepted] = "Accepted",
            [(int)IntakeRequestStatusAlias.Fulfilled] = "Fulfilled",
            [(int)IntakeRequestStatusAlias.Deferred] = "Deferred",
            [(int)IntakeRequestStatusAlias.Declined] = "Declined",
            [(int)IntakeRequestStatusAlias.Duplicate] = "Duplicate",
            [(int)IntakeRequestStatusAlias.Withdrawn] = "Withdrawn",
        },
        [
            (int)IntakeRequestStatusAlias.Submitted,
            (int)IntakeRequestStatusAlias.WaitingOnRequester,
            (int)IntakeRequestStatusAlias.Accepted,
            (int)IntakeRequestStatusAlias.Deferred,
            (int)IntakeRequestStatusAlias.Declined,
            (int)IntakeRequestStatusAlias.Duplicate,
            (int)IntakeRequestStatusAlias.Withdrawn,
        ],
        featureFlag: FeatureFlags.Names.DemandManagement);

    /// <summary>Every owner type this module contributes, for registration at startup.</summary>
    public static WorkflowOwnerDescriptor[] All => [IntakeRequest];

    /// <summary>
    /// Registers this module's owner types with the engine. Call once during startup, before anything
    /// resolves a workflow.
    /// </summary>
    public static void Register() => WorkflowOwners.Register(All);
}
