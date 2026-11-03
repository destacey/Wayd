namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>What a failed <see cref="WorkflowCheck"/> does.</summary>
public enum WorkflowCheckSeverity
{
    /// <summary>Refuses the publish or the assignment.</summary>
    Enforced = 1,

    /// <summary>Lets it through, and administration flags the workflow until the problem is fixed.</summary>
    Warned = 2,

    /// <summary>Not run: the check cannot fail, or has nothing to say, for this kind of workflow.</summary>
    Skipped = 3,
}
