using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Dtos;

/// <summary>A workflow as it appears in the list.</summary>
public sealed record StatusWorkflowListDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    /// <summary>The kind of record this workflow governs.</summary>
    public required WorkflowOwnerNavigationDto Owner { get; init; }

    public required string State { get; init; }
    public required bool IsSystem { get; init; }
    public required int StatusCount { get; init; }

    /// <summary>Whether any scope currently uses this workflow. Archiving is refused while it does.</summary>
    public required bool IsAssigned { get; init; }
}

/// <summary>One status within a workflow.</summary>
public sealed record WorkflowStatusDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    /// <summary>The rollup this status belongs to, as id and display name.</summary>
    public required SimpleNavigationDto Category { get; init; }

    /// <summary>The well-known meaning, or zero for none.</summary>
    public required int Alias { get; init; }

    /// <summary>The alias in the module's vocabulary, or null when it carries none.</summary>
    public string? AliasName { get; init; }

    public required int Order { get; init; }
}

/// <summary>A move a workflow allows, from one of its statuses to another.</summary>
public sealed record WorkflowTransitionDto
{
    /// <summary>The transition's own id, stable while the workflow keeps the move.</summary>
    public required Guid Id { get; init; }

    /// <summary>The status a record moves out of.</summary>
    public required Guid FromStatusId { get; init; }

    /// <summary>The status a record moves into.</summary>
    public required Guid ToStatusId { get; init; }

    /// <summary>
    /// What a record needs before making this move, in kind order. Empty when it needs nothing.
    /// </summary>
    public required IReadOnlyCollection<TransitionConditionKind> Conditions { get; init; }
}

/// <summary>
/// A move one of the owner type's actions makes — cut, release, withdraw — located in a workflow.
/// </summary>
public sealed record WorkflowMoveDto
{
    /// <summary>The status the move starts from.</summary>
    public required Guid FromStatusId { get; init; }

    /// <summary>The status the move ends in.</summary>
    public required Guid ToStatusId { get; init; }

    /// <summary>
    /// The actions that make the move, as the owner type names them. Usually one; more where the
    /// workflow's initial status carries an alias another action starts from.
    /// </summary>
    public required IReadOnlyCollection<string> Actions { get; init; }

    /// <summary>
    /// Whether a restricted workflow must allow the move. An optional one may be left out, and the
    /// action that makes it is then refused.
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// The conditions a transition along this move may carry: those the action can satisfy and something
    /// can check. Often none.
    /// </summary>
    public required IReadOnlyCollection<TransitionConditionKind> AcceptedConditions { get; init; }
}

/// <summary>A workflow with its statuses and what may currently be done to it.</summary>
public sealed record StatusWorkflowDetailsDto
{
    public required Guid Id { get; init; }
    public required int Key { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required WorkflowOwnerNavigationDto Owner { get; init; }
    public required string State { get; init; }
    public required bool IsSystem { get; init; }
    public required bool IsAssigned { get; init; }
    public required IReadOnlyCollection<WorkflowStatusDto> Statuses { get; init; }

    /// <summary>
    /// Whether records may move between any two statuses, or only along <see cref="Transitions"/>.
    /// </summary>
    public required TransitionMode TransitionMode { get; init; }

    /// <summary>
    /// The moves this workflow allows, ordered by their from and then their to status's position.
    /// Enforced only when <see cref="TransitionMode"/> is restricted.
    /// </summary>
    public required IReadOnlyCollection<WorkflowTransitionDto> Transitions { get; init; }

    /// <summary>
    /// The conditions a transition other than a declared move may carry: those a record checks
    /// itself, and those something is in place to evaluate. None when the owner type declares its moves,
    /// since no method makes such a transition. Attaching any other is refused.
    /// </summary>
    public required IReadOnlyCollection<TransitionConditionKind> AvailableConditions { get; init; }

    /// <summary>
    /// The moves this owner type's actions make, located in this workflow, in the order of their
    /// statuses. Empty when the owner type moves to any status it is given.
    /// </summary>
    /// <remarks>
    /// A restricted workflow must allow every required one before it can be published, so an editor
    /// shows those as fixed.
    /// </remarks>
    public required IReadOnlyCollection<WorkflowMoveDto> DeclaredMoves { get; init; }

    /// <summary>
    /// Whether this workflow's owner type can be restricted to its transitions. A restricted workflow for
    /// an owner type that cannot is refused at publish.
    /// </summary>
    public required bool SupportsRestrictedTransitions { get; init; }

    /// <summary>
    /// Why the transitions stop this workflow being published, one sentence per problem, each naming
    /// the status at fault. Empty when the workflow allows any move.
    /// </summary>
    public required IReadOnlyCollection<string> TransitionIssues { get; init; }

    /// <summary>
    /// The required aliases this workflow does not yet carry, named rather than numbered.
    /// </summary>
    /// <remarks>
    /// Resolved server-side because the numbers mean nothing to a reader: "needs a Released status"
    /// is actionable where "missing alias 3" is not.
    /// </remarks>
    public required IReadOnlyCollection<string> MissingRequiredAliases { get; init; }

    /// <summary>
    /// The categories this workflow's owner type requires that no status here belongs to yet.
    /// </summary>
    public required IReadOnlyCollection<StatusCategory> MissingRequiredCategories { get; init; }

    /// <summary>
    /// Every status whose alias belongs under a different category, one sentence each, naming the
    /// status. Publishing is refused until each is moved or loses its alias.
    /// </summary>
    public required IReadOnlyCollection<string> MisfiledAliases { get; init; }

    /// <summary>
    /// Whether the statuses may be restructured — Draft, and not platform-seeded.
    /// </summary>
    /// <remarks>
    /// The read side mirrors the domain rule so the UI can disable rather than let a user discover the
    /// refusal by hitting it. The aggregate remains the authority.
    /// </remarks>
    public required bool CanEdit { get; init; }

    public required bool CanPublish { get; init; }
    public required bool CanArchive { get; init; }
}

/// <summary>
/// An owner type, as referenced from a workflow or an assignment.
/// </summary>
/// <remarks>
/// Its own shape rather than <c>NavigationDto</c> because an owner type is keyed by string — a
/// registered descriptor key like <c>product.product</c>, not a database id.
/// </remarks>
public sealed record WorkflowOwnerNavigationDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
}

/// <summary>An owner type and the vocabulary its workflows are built from.</summary>
public sealed record WorkflowOwnerTypeDto
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Every alias this owner type recognises.</summary>
    public required IReadOnlyCollection<WorkflowAliasDto> Aliases { get; init; }

    /// <summary>The aliases a workflow must carry before it can be published.</summary>
    public required IReadOnlyCollection<int> RequiredAliases { get; init; }

    /// <summary>The categories a workflow must have at least one status in before it can be published.</summary>
    public required IReadOnlyCollection<StatusCategory> RequiredCategories { get; init; }

    /// <summary>Whether a workflow for this owner type may restrict which moves its records make.</summary>
    public required bool SupportsRestrictedTransitions { get; init; }
}

/// <summary>A well-known meaning a status of one owner type can carry.</summary>
public sealed record WorkflowAliasDto
{
    public required int Value { get; init; }
    public required string Name { get; init; }
    public required bool IsRequired { get; init; }

    /// <summary>The only category a status carrying this alias may belong to.</summary>
    public required StatusCategory Category { get; init; }
}

/// <summary>Which workflow a scope's records currently use.</summary>
public sealed record WorkflowAssignmentDto
{
    public required Guid Id { get; init; }
    public required WorkflowOwnerNavigationDto Owner { get; init; }

    /// <summary>Null for the organization-wide default, which is the only scope in use today.</summary>
    public Guid? ScopeId { get; init; }

    /// <summary>The workflow this scope's records use.</summary>
    public required NavigationDto Workflow { get; init; }
}
