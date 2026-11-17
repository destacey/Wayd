using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Changes everything about a KPI that is entered rather than measured — its descriptive fields, baseline and target —
/// in one save, so a change to several of them succeeds or fails as a whole.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
/// <param name="Name">What the KPI is called.</param>
/// <param name="Description">What it measures and why.</param>
/// <param name="CategoryId">An active category to group it under, its current one, or null.</param>
/// <param name="Prefix">A symbol shown before a value.</param>
/// <param name="Suffix">A unit shown after a value.</param>
/// <param name="ExpressedAs">Which reading the scorecard shows.</param>
/// <param name="Baseline">Where the KPI started.</param>
/// <param name="Target">What it aims for.</param>
public sealed record UpdateKpiCommand(
    string SubjectType,
    Guid SubjectId,
    Guid KpiId,
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs,
    KpiBaselineInput Baseline,
    KpiTargetInput Target)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class UpdateKpiCommandValidator : CustomValidator<UpdateKpiCommand>
{
    public UpdateKpiCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Kpi.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(Kpi.DescriptionMaxLength);
        RuleFor(c => c.Prefix).MaximumLength(Kpi.AffixMaxLength);
        RuleFor(c => c.Suffix).MaximumLength(Kpi.AffixMaxLength);
        RuleFor(c => c.ExpressedAs).IsInEnum();
        RuleFor(c => c.Baseline).NotNull();
        RuleFor(c => c.Target).NotNull();
        RuleFor(c => c.Target.Direction).IsInEnum().When(c => c.Target is not null);
        RuleFor(c => c.Target.TargetKind).IsInEnum().When(c => c.Target is not null);
    }
}

/// <summary>
/// Changes the details and the goal, saving only when both hold. A category the KPI already has may stay even when
/// inactive.
/// </summary>
public sealed class UpdateKpiCommandHandler(KpiCommandScope scope) : ICommandHandler<UpdateKpiCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(UpdateKpiCommand command, CancellationToken cancellationToken)
    {
        var kpi = await _scope.KpiForManaging(command.SubjectType, command.SubjectId, command.KpiId, withMeasurements: false, cancellationToken);
        if (kpi.IsFailure)
            return kpi;

        var category = await _scope.CheckCategoryAssignable(command.CategoryId, kpi.Value.CategoryId, cancellationToken);
        if (category.IsFailure)
            return category;

        var baseline = command.Baseline.ToBaseline();
        if (baseline.IsFailure)
            return baseline;

        var atRisk = command.Target.ToAtRisk();
        if (atRisk.IsFailure)
            return atRisk;

        var updated = kpi.Value.UpdateDetails(
            new KpiDetailsInput(command.Name, command.Description, command.CategoryId, command.Prefix, command.Suffix, command.ExpressedAs),
            _scope.Actor,
            _scope.Now);
        if (updated.IsFailure)
            return updated;

        var goal = new KpiGoal(command.Target.Direction, baseline.Value, command.Target.ToTarget(), atRisk.Value);
        var changed = kpi.Value.ChangeGoal(goal, _scope.Actor, _scope.Now);

        return changed.IsSuccess ? await _scope.Save(cancellationToken) : changed;
    }
}
