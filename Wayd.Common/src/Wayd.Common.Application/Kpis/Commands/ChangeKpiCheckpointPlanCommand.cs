using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Replaces an open KPI's checkpoint plan as a whole: checkpoints are matched by date, so one on a day already planned
/// is changed, one on a new day added and a planned day left out removed. An empty list clears the plan.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
/// <param name="Checkpoints">The whole plan, in any order.</param>
public sealed record ChangeKpiCheckpointPlanCommand(
    string SubjectType,
    Guid SubjectId,
    Guid KpiId,
    IReadOnlyList<KpiCheckpointValues> Checkpoints)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved. The domain checks the plan itself.</summary>
public sealed class ChangeKpiCheckpointPlanCommandValidator : CustomValidator<ChangeKpiCheckpointPlanCommand>
{
    public ChangeKpiCheckpointPlanCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiId).NotEmpty();
        RuleFor(c => c.Checkpoints).NotNull();
        RuleForEach(c => c.Checkpoints).NotNull().ChildRules(checkpoint =>
        {
            checkpoint.RuleFor(c => c.Label).NotEmpty().MaximumLength(KpiCheckpointPlan.LabelMaxLength);
        });
    }
}

/// <summary>Replaces the plan and saves.</summary>
public sealed class ChangeKpiCheckpointPlanCommandHandler(KpiCommandScope scope) : ICommandHandler<ChangeKpiCheckpointPlanCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(ChangeKpiCheckpointPlanCommand command, CancellationToken cancellationToken)
    {
        var kpi = await _scope.KpiForManaging(command.SubjectType, command.SubjectId, command.KpiId, withMeasurements: false, cancellationToken);
        if (kpi.IsFailure)
            return kpi;

        var before = kpi.Value.CheckpointPlan;
        var changed = kpi.Value.ChangeCheckpointPlan(command.Checkpoints, _scope.Actor, _scope.Now);
        if (changed.IsFailure)
            return changed;

        // Touching the row for an unchanged plan would bump its version and fail a concurrent editor's save for nothing.
        return before.SequenceEqual(kpi.Value.CheckpointPlan)
            ? Result.Success()
            : await _scope.SaveTouchingKpi(kpi.Value, cancellationToken);
    }
}
