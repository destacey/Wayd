using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Queries;

/// <summary>
/// A KPI's checkpoint plan in date order, each checkpoint with the measurement read against it. Null when the record
/// or the KPI does not exist, the KPI is on another record, or the caller cannot read the record.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
public sealed record GetKpiCheckpointPlanQuery(string SubjectType, Guid SubjectId, Guid KpiId)
    : IQuery<Result<IReadOnlyList<KpiCheckpointReadingDto>?>>;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class GetKpiCheckpointPlanQueryValidator : CustomValidator<GetKpiCheckpointPlanQuery>
{
    public GetKpiCheckpointPlanQueryValidator()
    {
        RuleFor(q => q.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.SubjectId).NotEmpty();
        RuleFor(q => q.KpiId).NotEmpty();
    }
}

/// <summary>Reads the plan and the measurements it judges once the record's subject type allows it.</summary>
public sealed class GetKpiCheckpointPlanQueryHandler(KpiSubjectAccessResolver accessResolver, IKpisDbContext dbContext)
    : IQueryHandler<GetKpiCheckpointPlanQuery, Result<IReadOnlyList<KpiCheckpointReadingDto>?>>
{
    private readonly KpiSubjectAccessResolver _accessResolver = accessResolver;
    private readonly IKpisDbContext _dbContext = dbContext;

    public async Task<Result<IReadOnlyList<KpiCheckpointReadingDto>?>> Handle(GetKpiCheckpointPlanQuery request, CancellationToken cancellationToken)
    {
        var resolved = await _accessResolver.Resolve(request.SubjectType, request.SubjectId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<IReadOnlyList<KpiCheckpointReadingDto>?>(resolved.Error);

        if (resolved.Value is not { } subject)
            return Result.Success<IReadOnlyList<KpiCheckpointReadingDto>?>(null);

        var subjectType = subject.SubjectType;
        var kpi = await _dbContext.Kpis
            .AsNoTracking()
            .Include(k => k.Measurements)
            .Include(k => k.Checkpoints)
            .FirstOrDefaultAsync(k => k.Id == request.KpiId && k.SubjectType == subjectType && k.SubjectId == request.SubjectId, cancellationToken);
        if (kpi is null)
            return Result.Success<IReadOnlyList<KpiCheckpointReadingDto>?>(null);

        var points = kpi.Measurements
            .OrderByDescending(m => m.PeriodEnd)
            .ThenByDescending(m => m.MeasuredAt)
            .Select(m => new KpiPointDto(m.PeriodStart, m.PeriodEnd, m.Value, m.MeasuredAt))
            .ToList();

        return Result.Success<IReadOnlyList<KpiCheckpointReadingDto>?>(KpiSummaries.CheckpointReadings(kpi, points));
    }
}
