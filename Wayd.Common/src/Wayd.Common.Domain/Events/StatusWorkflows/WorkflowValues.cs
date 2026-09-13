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
