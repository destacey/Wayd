using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Corrects a measurement on an open KPI. The person who took it stays as recorded.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
/// <param name="MeasurementId">The measurement's id.</param>
/// <param name="Measurement">The value and the period it describes.</param>
public sealed record ChangeKpiMeasurementCommand(
    string SubjectType,
    Guid SubjectId,
    Guid KpiId,
    Guid MeasurementId,
    KpiMeasurementValuesInput Measurement)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class ChangeKpiMeasurementCommandValidator : CustomValidator<ChangeKpiMeasurementCommand>
{
    public ChangeKpiMeasurementCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiId).NotEmpty();
        RuleFor(c => c.MeasurementId).NotEmpty();
        RuleFor(c => c.Measurement).NotNull().SetValidator(new KpiMeasurementValuesInputValidator());
    }
}

/// <summary>Changes the measurement and saves.</summary>
public sealed class ChangeKpiMeasurementCommandHandler(KpiCommandScope scope) : ICommandHandler<ChangeKpiMeasurementCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(ChangeKpiMeasurementCommand command, CancellationToken cancellationToken)
    {
        var kpi = await _scope.KpiForManaging(command.SubjectType, command.SubjectId, command.KpiId, withMeasurements: true, cancellationToken);
        if (kpi.IsFailure)
            return kpi;

        var existing = kpi.Value.Measurements.FirstOrDefault(m => m.Id == command.MeasurementId);
        if (existing is null)
            return Result.Failure(KpiAccessErrors.NotFound);

        // Who took the value stays; who corrected it is the event's actor.
        var zone = await _scope.SystemZone(cancellationToken);
        var input = command.Measurement.ToInput(existing.MeasuredById, zone);
        if (input.IsFailure)
            return input;

        var changed = kpi.Value.ChangeMeasurement(command.MeasurementId, input.Value, zone, _scope.Actor, _scope.Now);

        return changed.IsSuccess ? await _scope.SaveTouchingKpi(kpi.Value, cancellationToken) : changed;
    }
}
