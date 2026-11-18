using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Records a measurement against an open KPI, taken by the caller.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
/// <param name="Measurement">The value and the period it describes.</param>
public sealed record RecordKpiMeasurementCommand(string SubjectType, Guid SubjectId, Guid KpiId, KpiMeasurementValuesInput Measurement)
    : ICommand<Guid>, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class RecordKpiMeasurementCommandValidator : CustomValidator<RecordKpiMeasurementCommand>
{
    public RecordKpiMeasurementCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiId).NotEmpty();
        RuleFor(c => c.Measurement).NotNull().SetValidator(new KpiMeasurementValuesInputValidator());
    }
}

/// <summary>Records the measurement and saves, returning its id.</summary>
public sealed class RecordKpiMeasurementCommandHandler(KpiCommandScope scope) : ICommandHandler<RecordKpiMeasurementCommand, Guid>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result<Guid>> Handle(RecordKpiMeasurementCommand command, CancellationToken cancellationToken)
    {
        var kpi = await _scope.KpiForManaging(command.SubjectType, command.SubjectId, command.KpiId, withMeasurements: true, cancellationToken);
        if (kpi.IsFailure)
            return Result.Failure<Guid>(kpi.Error);

        var input = command.Measurement.ToInput(_scope.EmployeeId);
        if (input.IsFailure)
            return Result.Failure<Guid>(input.Error);

        var added = kpi.Value.AddMeasurement(input.Value, _scope.Actor, _scope.Now);
        if (added.IsFailure)
            return Result.Failure<Guid>(added.Error);

        var saved = await _scope.SaveMeasurements(kpi.Value, cancellationToken);
        return saved.IsSuccess ? added.Value.Id : Result.Failure<Guid>(saved.Error);
    }
}

/// <summary>The shape a measurement's input must have, shared by recording and changing one.</summary>
public sealed class KpiMeasurementValuesInputValidator : CustomValidator<KpiMeasurementValuesInput>
{
    public KpiMeasurementValuesInputValidator()
    {
        RuleFor(m => m.PeriodEnd).GreaterThanOrEqualTo(m => m.PeriodStart).WithMessage(KpiErrors.PeriodEndsBeforeStart);
        RuleFor(m => m.PeriodRecordKind).IsInEnum().When(m => m.PeriodRecordKind is not null);
        RuleFor(m => m.SampleSize).GreaterThanOrEqualTo(0).When(m => m.SampleSize is not null);
        RuleFor(m => m.Note).MaximumLength(KpiMeasurement.NoteMaxLength);
    }
}
