using Ardalis.GuardClauses;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// A move one of an owner type's named methods makes, declared on its
/// <see cref="WorkflowOwnerDescriptor"/> in terms of aliases, so the engine can check any workflow for it.
/// </summary>
/// <remarks>
/// An owner type that declares its moves changes status only through methods that resolve aliases — cut,
/// release, withdraw — so the statuses its records can enter, and the moves between them, are known in
/// code. A restricted workflow for it must allow every <see cref="IsRequired"/> move, and may leave out
/// the rest; which is which is the module's call.
/// <para>
/// Not a record: <see cref="AcceptedConditions"/> would compare by reference.
/// </para>
/// </remarks>
public sealed class WorkflowMove
{
    private WorkflowMove(string action, int? fromAlias, int? toAlias, bool isRequired, IReadOnlyCollection<TransitionConditionKind>? acceptedConditions)
    {
        Action = Guard.Against.NullOrWhiteSpace(action, nameof(action)).Trim();

        if (fromAlias == StatusWorkflow.NoAlias || toAlias == StatusWorkflow.NoAlias)
        {
            throw new ArgumentException($"'{Action}' names NoAlias as an end. A move is between aliased statuses, or to or from the initial status.");
        }

        if (fromAlias == toAlias)
        {
            throw new ArgumentException($"'{Action}' moves from a status to itself, which is not a move.");
        }

        var accepted = acceptedConditions ?? [];
        if (accepted.Any(kind => !Enum.IsDefined(kind)) || accepted.Distinct().Count() != accepted.Count)
        {
            throw new ArgumentException($"'{Action}' lists an unknown or repeated condition.", nameof(acceptedConditions));
        }

        FromAlias = fromAlias;
        ToAlias = toAlias;
        IsRequired = isRequired;
        AcceptedConditions = accepted.ToHashSet();
    }

    /// <summary>
    /// What the move is called, after the method that makes it (<c>Withdraw</c>), for messages and
    /// for an administrator reading the transition grid.
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// The alias the move starts from, or <c>null</c> for the workflow's initial status — where every
    /// record starts, whatever alias it carries.
    /// </summary>
    public int? FromAlias { get; }

    /// <summary>
    /// The alias the move ends in, or <c>null</c> for the workflow's initial status — where a record
    /// returns when a method undoes the only step it took.
    /// </summary>
    public int? ToAlias { get; }

    /// <summary>
    /// Whether a restricted workflow must allow this move. A required move is one the domain depends on —
    /// a deployment's failure, say, which the delivery figures count — and an optional one is a company's
    /// choice to forbid.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// The conditions a workflow may attach to this move. Empty unless the method making it can satisfy
    /// them: a method that takes no reason would be refused every time by a move that needs one.
    /// </summary>
    public IReadOnlySet<TransitionConditionKind> AcceptedConditions { get; }

    /// <summary>A move a restricted workflow must allow.</summary>
    public static WorkflowMove Required(string action, int? fromAlias, int? toAlias, params TransitionConditionKind[] acceptedConditions) =>
        new(action, fromAlias, toAlias, isRequired: true, acceptedConditions);

    /// <summary>A move a restricted workflow may leave out, refusing the method that makes it.</summary>
    public static WorkflowMove Optional(string action, int? fromAlias, int? toAlias, params TransitionConditionKind[] acceptedConditions) =>
        new(action, fromAlias, toAlias, isRequired: false, acceptedConditions);
}

/// <summary>
/// The declared moves that land on one pair of statuses in one workflow.
/// </summary>
/// <remarks>
/// Usually one move. More than one when the workflow's initial status carries an alias a move also
/// starts from, so two declarations resolve to the same pair; the pair is then required if any of them
/// is, and accepts only the conditions all of them accept, since either method may make it.
/// <para>
/// Not a record, for the same reason as <see cref="WorkflowMove"/>. Compare
/// <see cref="FromStatusId"/> and <see cref="ToStatusId"/>.
/// </para>
/// </remarks>
public sealed class ResolvedWorkflowMove
{
    internal ResolvedWorkflowMove(Guid fromStatusId, Guid toStatusId, IReadOnlyList<WorkflowMove> moves)
    {
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        Moves = moves;
        IsRequired = moves.Any(m => m.IsRequired);
        AcceptedConditions = moves
            .Skip(1)
            .Aggregate(moves[0].AcceptedConditions.ToHashSet(), (accepted, m) => { accepted.IntersectWith(m.AcceptedConditions); return accepted; });
    }

    /// <summary>The status the move starts from in this workflow.</summary>
    public Guid FromStatusId { get; }

    /// <summary>The status the move ends in in this workflow.</summary>
    public Guid ToStatusId { get; }

    /// <summary>The declarations that resolve to this pair, in the order the owner type declares them.</summary>
    public IReadOnlyList<WorkflowMove> Moves { get; }

    /// <summary>Whether a restricted workflow must allow this pair.</summary>
    public bool IsRequired { get; }

    /// <summary>The conditions a transition along this pair may carry.</summary>
    public IReadOnlySet<TransitionConditionKind> AcceptedConditions { get; }

    /// <summary>The actions that make this move, joined for a message.</summary>
    public string DescribeActions() => string.Join(", ", Moves.Select(m => m.Action));
}
