using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Conditions;

/// <summary>
/// Evaluates the transition conditions a record cannot check itself, before the record is asked to move.
/// </summary>
/// <remarks>
/// The aggregate has no I/O, so a condition such as a passed approval is decided here and handed to it on
/// the <see cref="StatusRef"/> (<see cref="StatusRef.MetConditions"/>). The aggregate still makes the call
/// and names what is missing; this only supplies what it cannot find out. Conditions it can check — a
/// reason — never come through here.
/// </remarks>
public interface ITransitionConditionChecker : IScopedService
{
    /// <summary>
    /// Whether a transition may carry <paramref name="kind"/>: the record checks it itself, or an evaluator
    /// is registered for it.
    /// </summary>
    bool IsAvailable(TransitionConditionKind kind);

    /// <summary>
    /// Evaluates what a move from <paramref name="fromStatusId"/> into <paramref name="target"/> needs that
    /// the record cannot check, returning the target with those found met.
    /// </summary>
    /// <param name="ownerType">The registered owner type of the record.</param>
    /// <param name="recordId">The record moving.</param>
    /// <param name="fromStatusId">The status it holds now.</param>
    /// <param name="target">The status it is moving to, from <see cref="StatusWorkflow.TargetRef"/>.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    Task<StatusRef> Evaluate(string ownerType, Guid recordId, Guid fromStatusId, StatusRef target, CancellationToken cancellationToken);
}
