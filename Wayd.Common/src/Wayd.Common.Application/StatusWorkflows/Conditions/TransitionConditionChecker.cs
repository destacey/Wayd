using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Conditions;

/// <inheritdoc cref="ITransitionConditionChecker"/>
public sealed class TransitionConditionChecker : ITransitionConditionChecker
{
    private readonly Dictionary<TransitionConditionKind, ITransitionConditionEvaluator> _evaluators;

    /// <exception cref="InvalidOperationException">
    /// Two evaluators claim one kind, or one claims a kind the record checks itself — either is a
    /// registration mistake that would otherwise decide moves arbitrarily.
    /// </exception>
    public TransitionConditionChecker(IEnumerable<ITransitionConditionEvaluator> evaluators)
    {
        _evaluators = [];

        foreach (var evaluator in evaluators)
        {
            if (evaluator.Kind.IsSelfChecked())
            {
                throw new InvalidOperationException($"{evaluator.Kind} is checked by the record; it takes no evaluator.");
            }

            if (!_evaluators.TryAdd(evaluator.Kind, evaluator))
            {
                throw new InvalidOperationException($"More than one evaluator is registered for {evaluator.Kind}.");
            }
        }
    }

    public bool IsAvailable(TransitionConditionKind kind) => kind.IsSelfChecked() || _evaluators.ContainsKey(kind);

    public async Task<StatusRef> Evaluate(
        string ownerType, Guid recordId, Guid fromStatusId, StatusRef target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var move = new TransitionConditionMove(ownerType, recordId, fromStatusId, target);
        var met = new List<TransitionConditionKind>();

        // A kind with no evaluator is left unmet: the record then refuses the move, naming the condition.
        foreach (var kind in target.ConditionsFor(fromStatusId).Where(k => !k.IsSelfChecked()).Order())
        {
            if (_evaluators.TryGetValue(kind, out var evaluator) && await evaluator.IsMet(move, cancellationToken))
            {
                met.Add(kind);
            }
        }

        return met.Count == 0 ? target : target.WithMetConditions(met);
    }
}
