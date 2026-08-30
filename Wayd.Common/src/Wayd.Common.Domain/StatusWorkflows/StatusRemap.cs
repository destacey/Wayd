using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// How each status in one workflow translates to a status in another, so a scope can be moved between
/// workflows without leaving its records holding a status their workflow does not contain.
/// </summary>
/// <remarks>
/// <para>
/// Statuses are never shared between workflows — cloning copies them with fresh ids — so every record
/// in a scope has to be moved when its workflow changes. This decides where each one goes.
/// </para>
/// <para>
/// The switch is <strong>validated, not repaired</strong>: a reassignment is refused until every status
/// is mapped, so a record is never left stranded. That is Azure DevOps' guarantee rather than Jira's,
/// where a scheme change is permitted and issues can end up in a status their workflow no longer has.
/// </para>
/// <para>
/// A value, not a process. Building it is separate from applying it, which is what lets a large
/// migration be reviewed before it runs, resume after an interruption, and translate a record that
/// arrives late.
/// </para>
/// </remarks>
public sealed class StatusRemap
{
    private readonly Dictionary<Guid, StatusRef> _decisions = [];
    private readonly List<WorkflowStatus> _unresolved = [];

    private StatusRemap(Guid fromWorkflowId, Guid toWorkflowId)
    {
        FromWorkflowId = fromWorkflowId;
        ToWorkflowId = toWorkflowId;
    }

    /// <summary>The workflow being moved away from.</summary>
    public Guid FromWorkflowId { get; }

    /// <summary>The workflow being moved to.</summary>
    public Guid ToWorkflowId { get; }

    /// <summary>
    /// The statuses still needing a human decision, in display order. Empty when the remap is complete.
    /// </summary>
    public IReadOnlyCollection<WorkflowStatus> Unresolved => _unresolved.AsReadOnly();

    /// <summary>
    /// Whether every status in the source workflow has somewhere to go.
    /// </summary>
    public bool IsComplete => _unresolved.Count == 0;

    /// <summary>
    /// Where a status translates to, or <c>null</c> when it is still unresolved.
    /// </summary>
    public StatusRef? For(Guid fromStatusId) =>
        _decisions.TryGetValue(fromStatusId, out var target) ? target : null;

    /// <summary>
    /// Decides where a status goes, replacing any automatic choice.
    /// </summary>
    public Result Resolve(Guid fromStatusId, WorkflowStatus target)
    {
        Guard.Against.Null(target, nameof(target));

        if (target.WorkflowId != ToWorkflowId)
        {
            return Result.Failure("A status can only be mapped to one in the workflow being moved to.");
        }

        if (!_decisions.ContainsKey(fromStatusId) && _unresolved.All(s => s.Id != fromStatusId))
        {
            return Result.Failure("That status is not in the workflow being moved from.");
        }

        _decisions[fromStatusId] = StatusRef.From(target);
        _unresolved.RemoveAll(s => s.Id == fromStatusId);

        return Result.Success();
    }

    /// <summary>
    /// Builds a mapping between two workflows, deciding automatically what it can.
    /// </summary>
    /// <remarks>
    /// Three passes, most reliable first. <strong>Alias</strong> is unambiguous — a status meaning
    /// Released maps to whatever means Released in the target, whatever either is called — and is why
    /// most of a remap needs no human at all. <strong>Exact name</strong> catches the statuses an
    /// organization invented and kept. <strong>Category</strong> is the last resort and only fires when
    /// the target has exactly one status in that category, since picking arbitrarily between several
    /// would be a guess dressed as a decision. Anything left over is
    /// <see cref="Unresolved"/> and needs a person.
    /// </remarks>
    public static Result<StatusRemap> AutoMap(StatusWorkflow from, StatusWorkflow to)
    {
        Guard.Against.Null(from, nameof(from));
        Guard.Against.Null(to, nameof(to));

        if (from.Id == to.Id)
        {
            return Result.Failure<StatusRemap>("A workflow does not need remapping to itself.");
        }

        if (!string.Equals(from.OwnerType, to.OwnerType, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<StatusRemap>(
                $"A {from.OwnerType} workflow cannot be remapped to a {to.OwnerType} one.");
        }

        var remap = new StatusRemap(from.Id, to.Id);
        var targets = to.Statuses.ToList();

        foreach (var source in from.Statuses)
        {
            var match =
                MatchByAlias(source, targets)
                ?? MatchByName(source, targets)
                ?? MatchByCategory(source, targets);

            if (match is null)
            {
                remap._unresolved.Add(source);
                continue;
            }

            remap._decisions[source.Id] = StatusRef.From(match);
        }

        return Result.Success(remap);
    }

    private static WorkflowStatus? MatchByAlias(WorkflowStatus source, List<WorkflowStatus> targets) =>
        source.Alias == StatusWorkflow.NoAlias
            ? null
            : targets.SingleOrDefault(t => t.Alias == source.Alias);

    private static WorkflowStatus? MatchByName(WorkflowStatus source, List<WorkflowStatus> targets) =>
        targets.SingleOrDefault(t => string.Equals(t.Name, source.Name, StringComparison.OrdinalIgnoreCase));

    private static WorkflowStatus? MatchByCategory(WorkflowStatus source, List<WorkflowStatus> targets)
    {
        var candidates = targets.Where(t => t.Category == source.Category).ToList();

        // Only when there is no choice to make. Several candidates is a decision, not a match.
        return candidates.Count == 1 ? candidates[0] : null;
    }
}
