using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Queries;

/// <summary>
/// A record's scorecard: every active KPI on it, with its latest reading and recent values. Null when the record does
/// not exist or the caller cannot read it.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
public sealed record GetKpiScorecardQuery(string SubjectType, Guid SubjectId) : IQuery<Result<KpiScorecardDto?>>;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class GetKpiScorecardQueryValidator : CustomValidator<GetKpiScorecardQuery>
{
    public GetKpiScorecardQueryValidator()
    {
        RuleFor(q => q.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.SubjectId).NotEmpty();
    }
}

/// <summary>Reads the active KPIs once the record's subject type allows it.</summary>
public sealed class GetKpiScorecardQueryHandler(KpiSubjectAccessResolver accessResolver, IKpisDbContext dbContext)
    : IQueryHandler<GetKpiScorecardQuery, Result<KpiScorecardDto?>>
{
    private readonly KpiSubjectAccessResolver _accessResolver = accessResolver;
    private readonly IKpisDbContext _dbContext = dbContext;

    public async Task<Result<KpiScorecardDto?>> Handle(GetKpiScorecardQuery request, CancellationToken cancellationToken)
    {
        var resolved = await _accessResolver.Resolve(request.SubjectType, request.SubjectId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<KpiScorecardDto?>(resolved.Error);

        if (resolved.Value is not { } subject)
            return Result.Success<KpiScorecardDto?>(null);

        var subjectType = subject.SubjectType;
        var kpis = await KpiSummaries.For(
            _dbContext,
            _dbContext.Kpis.Where(k => k.SubjectType == subjectType && k.SubjectId == request.SubjectId && k.Status == KpiStatus.Active),
            cancellationToken);

        return new KpiScorecardDto
        {
            SubjectType = subjectType,
            SubjectId = request.SubjectId,
            SubjectName = subject.Access.Name,
            SubjectCode = subject.Access.Code,
            CanManage = subject.Access.CanManage,
            Kpis = kpis,
        };
    }
}
