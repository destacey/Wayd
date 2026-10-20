using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Events.StatusWorkflows;

/// <summary>
/// A workflow's editable details taken together, as <see cref="WorkflowDetailsUpdatedEvent.Previous"/> records
/// the values an edit replaced.
/// </summary>
public sealed record WorkflowDetails(string Name, string? Description);

/// <summary>
/// A status's name and description taken together, as <see cref="WorkflowStatusRenamedEvent.Previous"/>
/// records the values a rename replaced.
/// </summary>
public sealed record WorkflowStatusDetails(string Name, string? Description);

/// <summary>
/// One status as a workflow was created with it, carried by <see cref="WorkflowCreatedEvent"/>.
/// </summary>
/// <param name="Alias">
/// The well-known meaning, in the vocabulary of the workflow's owner type; 0 for none.
/// </param>
public sealed record WorkflowStatusValues(
    Guid StatusId,
    string Name,
    string? Description,
    StatusCategory Category,
    int Alias,
    int Order);

/// <summary>
/// One move a workflow allows, as carried by <see cref="WorkflowTransitionsChangedEvent"/> and
/// <see cref="WorkflowCreatedEvent"/>.
/// </summary>
/// <param name="Conditions">
/// What a record needs before making the move, in kind order. Added with transition conditions; a
/// payload written before them reads as empty, which is what every move had then.
/// </param>
public sealed record WorkflowTransitionValues(
    Guid TransitionId,
    Guid FromStatusId,
    Guid ToStatusId,
    WorkflowTransitionConditionValues[]? Conditions = null)
{
    /// <inheritdoc cref="WorkflowTransitionValues" path="/param[@name='Conditions']"/>
    public WorkflowTransitionConditionValues[] Conditions { get; init; } = Conditions ?? [];
}

/// <summary>
/// One condition on a move a workflow allows.
/// </summary>
public sealed record WorkflowTransitionConditionValues(Guid ConditionId, TransitionConditionKind Kind);

/// <summary>
/// A kept move whose conditions an edit changed, with the conditions before and after.
/// </summary>
public sealed record WorkflowTransitionConditionsChange(
    Guid TransitionId,
    Guid FromStatusId,
    Guid ToStatusId,
    WorkflowTransitionConditionValues[] PreviousConditions,
    WorkflowTransitionConditionValues[] Conditions);
