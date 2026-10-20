using Wayd.Common.Application.StatusWorkflows.Conditions;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Conditions;

/// <summary>An evaluator that answers one fixed way, recording each move it was asked about.</summary>
internal sealed class FakeConditionEvaluator(TransitionConditionKind kind, bool isMet) : ITransitionConditionEvaluator
{
    private readonly bool _isMet = isMet;

    public TransitionConditionKind Kind { get; } = kind;

    public List<TransitionConditionMove> Asked { get; } = [];

    public Task<bool> IsMet(TransitionConditionMove move, CancellationToken cancellationToken)
    {
        Asked.Add(move);

        return Task.FromResult(_isMet);
    }
}
