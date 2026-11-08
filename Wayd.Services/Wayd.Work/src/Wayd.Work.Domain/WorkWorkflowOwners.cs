using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Domain;

/// <summary>
/// The kinds of Work record the status workflow engine governs.
/// </summary>
/// <remarks>
/// The key is persisted on every workflow, record type, scheme and assignment row.
/// <strong>Never change it once shipped</strong>: renaming it orphans them.
/// </remarks>
public static class WorkWorkflowOwners
{
    /// <summary>A work item. Its type (Story, Bug) decides its workflow within its workspace.</summary>
    /// <remarks>
    /// A workspace is the only scope below the organization, and every synced workspace is assigned the
    /// scheme of its source process.
    /// <para>
    /// No aliases: nothing in Work resolves a status by meaning. Every rule a work item follows — when it
    /// counts as started, finished or dropped — is written against its category.
    /// </para>
    /// <para>
    /// Proposed, Active and Done are required of a workflow Wayd owns, so a native work item can wait, be
    /// worked on and be finished. A synced workflow missing one still syncs and is only warned about
    /// (<see cref="WorkflowCheckRules"/>): its source decides its statuses.
    /// </para>
    /// <para>
    /// Restricted transitions are supported for owned workflows. A managed one always allows any move, since
    /// the source has already made it.
    /// </para>
    /// </remarks>
    public static readonly WorkflowOwnerDescriptor WorkItem = new(
        "work.item",
        "Work Item",
        new Dictionary<int, WorkflowAlias>(),
        [],
        [StatusCategory.Proposed, StatusCategory.Active, StatusCategory.Done],
        supportsRestrictedTransitions: true,
        hasRecordTypes: true,
        hasManagedWorkflows: true);

    /// <summary>Every owner type this module contributes, for registration at startup.</summary>
    public static WorkflowOwnerDescriptor[] All => [WorkItem];

    /// <summary>
    /// Registers this module's owner types with the engine. Call once during startup, before anything
    /// resolves a workflow.
    /// </summary>
    public static void Register() => WorkflowOwners.Register(All);
}
