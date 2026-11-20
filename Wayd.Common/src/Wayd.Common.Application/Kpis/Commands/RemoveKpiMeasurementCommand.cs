using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Removes a measurement from an open KPI.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
/// <param name="MeasurementId">The measurement's id.</param>
public sealed record RemoveKpiMeasurementCommand(string SubjectType, Guid SubjectId, Guid KpiId, Guid MeasurementId)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class RemoveKpiMeasurementCommandValidator : CustomValidator<RemoveKpiMeasurementCommand>
{
    public RemoveKpiMeasurementCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiId).NotEmpty();
        RuleFor(c => c.MeasurementId).NotEmpty();
    }
}

/// <summary>Removes the measurement and saves.</summary>
public sealed class RemoveKpiMeasurementCommandHandler(KpiCommandScope scope) : ICommandHandler<RemoveKpiMeasurementCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(RemoveKpiMeasurementCommand command, CancellationToken cancellationToken)
    {
        var kpi = await _scope.KpiForManaging(command.SubjectType, command.SubjectId, command.KpiId, withMeasurements: true, cancellationToken);
        if (kpi.IsFailure)
            return kpi;

        if (kpi.Value.Measurements.All(m => m.Id != command.MeasurementId))
            return Result.Failure(KpiAccessErrors.NotFound);

        var removed = kpi.Value.RemoveMeasurement(command.MeasurementId, _scope.Actor, _scope.Now);

        return removed.IsSuccess ? await _scope.SaveTouchingKpi(kpi.Value, cancellationToken) : removed;
    }
}
