using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Adds a KPI to a record, last on its scorecard. Only someone who may manage the record's KPIs.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="Name">What the KPI is called.</param>
/// <param name="Description">What it measures and why.</param>
/// <param name="CategoryId">An active category to group it under, or null.</param>
/// <param name="Prefix">A symbol shown before a value.</param>
/// <param name="Suffix">A unit shown after a value.</param>
/// <param name="ExpressedAs">Which reading the scorecard shows.</param>
/// <param name="Baseline">Where the KPI started.</param>
/// <param name="Target">What it aims for.</param>
public sealed record AddKpiCommand(
    string SubjectType,
    Guid SubjectId,
    string Name,
    string? Description,
    Guid? CategoryId,
    string? Prefix,
    string? Suffix,
    KpiExpressedAs ExpressedAs,
    KpiBaselineInput Baseline,
    KpiTargetInput Target)
    : ICommand<ObjectIdAndKey>, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class AddKpiCommandValidator : CustomValidator<AddKpiCommand>
{
    public AddKpiCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
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

/// <summary>Adds the KPI after the record's subject type allows it, and saves.</summary>
public sealed class AddKpiCommandHandler(KpiCommandScope scope) : ICommandHandler<AddKpiCommand, ObjectIdAndKey>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result<ObjectIdAndKey>> Handle(AddKpiCommand command, CancellationToken cancellationToken)
    {
        var subject = await _scope.ForManaging(command.SubjectType, command.SubjectId, cancellationToken);
        if (subject.IsFailure)
            return Result.Failure<ObjectIdAndKey>(subject.Error);

        var category = await _scope.CheckCategoryAssignable(command.CategoryId, null, cancellationToken);
        if (category.IsFailure)
            return Result.Failure<ObjectIdAndKey>(category.Error);

        var baseline = command.Baseline.ToBaseline();
        if (baseline.IsFailure)
            return Result.Failure<ObjectIdAndKey>(baseline.Error);

        var atRisk = command.Target.ToAtRisk();
        if (atRisk.IsFailure)
            return Result.Failure<ObjectIdAndKey>(atRisk.Error);

        var goal = new KpiGoal(command.Target.Direction, baseline.Value, command.Target.ToTarget(), atRisk.Value);
        var definition = new KpiDefinition(
            command.Name, command.Description, command.CategoryId, command.Prefix, command.Suffix, command.ExpressedAs, goal);

        var subjectType = subject.Value.SubjectType;
        var orders = await _scope.DbContext.Kpis
            .Where(k => k.SubjectType == subjectType && k.SubjectId == command.SubjectId)
            .Select(k => (int?)k.Order)
            .MaxAsync(cancellationToken);

        var created = Kpi.Create(subjectType, command.SubjectId, definition, (orders ?? -1) + 1, _scope.Actor, _scope.Now);
        if (created.IsFailure)
            return Result.Failure<ObjectIdAndKey>(created.Error);

        _scope.DbContext.Kpis.Add(created.Value);

        var saved = await _scope.Save(cancellationToken);
        return saved.IsSuccess
            ? new ObjectIdAndKey(created.Value.Id, created.Value.Key)
            : Result.Failure<ObjectIdAndKey>(saved.Error);
    }
}
