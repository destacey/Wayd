using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Commands;

/// <summary>
/// Puts a record's KPIs in a new order on its scorecard.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiIds">Every KPI on the record, open and closed, each once, in their new order.</param>
public sealed record ReorderKpisCommand(string SubjectType, Guid SubjectId, IReadOnlyList<Guid> KpiIds)
    : ICommand, IRequireLinkedEmployee;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class ReorderKpisCommandValidator : CustomValidator<ReorderKpisCommand>
{
    public ReorderKpisCommandValidator()
    {
        RuleFor(c => c.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(c => c.SubjectId).NotEmpty();
        RuleFor(c => c.KpiIds).NotNull();
    }
}

/// <summary>Reorders every KPI on the record and saves.</summary>
public sealed class ReorderKpisCommandHandler(KpiCommandScope scope) : ICommandHandler<ReorderKpisCommand>
{
    private readonly KpiCommandScope _scope = scope;

    public async Task<Result> Handle(ReorderKpisCommand command, CancellationToken cancellationToken)
    {
        var subject = await _scope.ForManaging(command.SubjectType, command.SubjectId, cancellationToken);
        if (subject.IsFailure)
            return subject;

        var subjectType = subject.Value.SubjectType;
        var kpis = await _scope.DbContext.Kpis
            .Where(k => k.SubjectType == subjectType && k.SubjectId == command.SubjectId)
            .ToListAsync(cancellationToken);

        var reordered = Kpi.Reorder(kpis, command.KpiIds, _scope.Actor, _scope.Now);

        return reordered.IsSuccess ? await _scope.Save(cancellationToken) : reordered;
    }
}
