using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Removes a KPI and its measurements for good. A KPI whose history should stay is closed instead.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
public sealed record RemoveKpiCommand(string SubjectType, Guid SubjectId, Guid KpiId)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class RemoveKpiCommandValidator : CustomValidator<RemoveKpiCommand>
{
    public RemoveKpiCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiId).NotEmpty();
    }
}

/// <summary>Removes the KPI and saves; the database removes its measurements.</summary>
public sealed class RemoveKpiCommandHandler(KpiCommandScope scope) : ICommandHandler<RemoveKpiCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(RemoveKpiCommand command, CancellationToken cancellationToken)
    {
        var kpi = await _scope.KpiForManaging(command.SubjectType, command.SubjectId, command.KpiId, withMeasurements: false, cancellationToken);
        if (kpi.IsFailure)
            return kpi;

        kpi.Value.Remove(_scope.Actor, _scope.Now);
        _scope.DbContext.Kpis.Remove(kpi.Value);

        return await _scope.Save(cancellationToken);
    }
}
