using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Closes a KPI, taking it off the scorecard with its measurements kept, or reopens a closed one.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
/// <param name="Status">The status to move it to.</param>
public sealed record SetKpiStatusCommand(string SubjectType, Guid SubjectId, Guid KpiId, KpiStatus Status)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class SetKpiStatusCommandValidator : CustomValidator<SetKpiStatusCommand>
{
    public SetKpiStatusCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}

/// <summary>Closes or reopens and saves.</summary>
public sealed class SetKpiStatusCommandHandler(KpiCommandScope scope) : ICommandHandler<SetKpiStatusCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(SetKpiStatusCommand command, CancellationToken cancellationToken)
    {
        var kpi = await _scope.KpiForManaging(command.SubjectType, command.SubjectId, command.KpiId, withMeasurements: false, cancellationToken);
        if (kpi.IsFailure)
            return kpi;

        var changed = command.Status == KpiStatus.Closed
            ? kpi.Value.Close(_scope.Actor, _scope.Now)
            : kpi.Value.Reopen(_scope.Actor, _scope.Now);

        return changed.IsSuccess ? await _scope.Save(cancellationToken) : changed;
    }
}
