using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// Which of the engine's checks apply to owned and to managed workflows, and how hard. The one place that
/// says so: <see cref="StatusWorkflow"/> reads it, and nothing else decides a check's severity.
/// </summary>
/// <remarks>
/// An owned workflow is authored in Wayd, so every check is enforced: an administrator can fix what it
/// names before anything depends on it. A managed workflow is authored by its source, and the sync has to
/// follow it without refusing. Its structure checks only warn, so a workflow missing a required category
/// still syncs and administration flags it; the aliases are Wayd's to add, so a missing one is a warning
/// too. It never restricts transitions, so that check is skipped. The checks that protect records already
/// on a workflow (only a published one is assigned, every held status is remapped) hold for both.
/// </remarks>
public static class WorkflowCheckRules
{
    private static readonly Dictionary<WorkflowCheck, (WorkflowCheckSeverity Owned, WorkflowCheckSeverity Managed)> Rules = new()
    {
        [WorkflowCheck.RequiredCategories] = (WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Warned),
        [WorkflowCheck.RequiredAliases] = (WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Warned),
        [WorkflowCheck.AliasCategories] = (WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Warned),
        [WorkflowCheck.Transitions] = (WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Skipped),
        [WorkflowCheck.Published] = (WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Enforced),
        [WorkflowCheck.CompleteRemap] = (WorkflowCheckSeverity.Enforced, WorkflowCheckSeverity.Enforced),
    };

    /// <summary>What a failed <paramref name="check"/> does to a workflow of <paramref name="ownership"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A check or ownership the table does not list.</exception>
    public static WorkflowCheckSeverity For(WorkflowCheck check, WorkflowOwnership ownership)
    {
        if (!Rules.TryGetValue(check, out var rule))
        {
            throw new ArgumentOutOfRangeException(nameof(check), check, "Every workflow check needs a rule.");
        }

        return ownership switch
        {
            WorkflowOwnership.Owned => rule.Owned,
            WorkflowOwnership.Managed => rule.Managed,
            _ => throw new ArgumentOutOfRangeException(nameof(ownership), ownership, "Unknown workflow ownership."),
        };
    }
}
