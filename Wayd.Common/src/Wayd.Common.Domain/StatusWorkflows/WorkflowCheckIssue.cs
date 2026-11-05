using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// One problem a <see cref="WorkflowCheck"/> found with a workflow, and what it does to that workflow under
/// <see cref="WorkflowCheckRules"/>.
/// </summary>
/// <param name="Message">A sentence naming the problem and how to fix it, for an administrator.</param>
public sealed record WorkflowCheckIssue(WorkflowCheck Check, WorkflowCheckSeverity Severity, string Message);
