using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.StatusWorkflows.Conditions;

/// <summary>
/// Decides whether one kind of transition condition is met, for a condition a record cannot check itself.
/// </summary>
/// <remarks>
/// One implementation per <see cref="Kind"/>, registered through the <see cref="IScopedService"/> scan, and
/// reached only through <see cref="ITransitionConditionChecker"/>. A kind with no evaluator cannot be
/// attached to a transition, and a move needing one is refused.
/// </remarks>
public interface ITransitionConditionEvaluator : IScopedService
{
    /// <summary>The condition this evaluator decides. Never <see cref="TransitionConditionKinds.IsSelfChecked"/>.</summary>
    TransitionConditionKind Kind { get; }

    /// <summary>Whether the move described by <paramref name="move"/> meets the condition.</summary>
    Task<bool> IsMet(TransitionConditionMove move, CancellationToken cancellationToken);
}

/// <summary>A move a record is about to make, as an evaluator sees it.</summary>
/// <param name="OwnerType">The registered owner type of the record.</param>
/// <param name="RecordId">The record moving.</param>
/// <param name="FromStatusId">The status it holds now.</param>
/// <param name="Target">The status it is moving to, as its workflow resolved it.</param>
public sealed record TransitionConditionMove(string OwnerType, Guid RecordId, Guid FromStatusId, StatusRef Target);
