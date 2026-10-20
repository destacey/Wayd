using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A move a workflow should allow, with what a record needs to make it, as
/// <see cref="StatusWorkflow.SetTransitions"/> takes it.
/// </summary>
/// <param name="Conditions">The conditions the move should carry; <c>null</c> or empty for none.</param>
public sealed record WorkflowTransitionDefinition(
    Guid FromStatusId,
    Guid ToStatusId,
    IReadOnlyCollection<TransitionConditionKind>? Conditions = null);
