using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Queries;

/// <summary>
/// A record's closed KPIs, kept as history, ordered as a scorecard orders them. Null when the record does not exist or
/// the caller cannot read it.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
public sealed record GetClosedKpisQuery(string SubjectType, Guid SubjectId) : IQuery<Result<List<KpiSummaryDto>?>>;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class GetClosedKpisQueryValidator : CustomValidator<GetClosedKpisQuery>
{
    public GetClosedKpisQueryValidator()
    {
        RuleFor(q => q.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.SubjectId).NotEmpty();
    }
}

/// <summary>Reads the closed KPIs once the record's subject type allows it.</summary>
public sealed class GetClosedKpisQueryHandler(KpiSubjectAccessResolver accessResolver, IKpisDbContext dbContext)
    : IQueryHandler<GetClosedKpisQuery, Result<List<KpiSummaryDto>?>>
{
    private readonly KpiSubjectAccessResolver _accessResolver = accessResolver;
    private readonly IKpisDbContext _dbContext = dbContext;

    public async Task<Result<List<KpiSummaryDto>?>> Handle(GetClosedKpisQuery request, CancellationToken cancellationToken)
    {
        var resolved = await _accessResolver.Resolve(request.SubjectType, request.SubjectId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<List<KpiSummaryDto>?>(resolved.Error);

        if (resolved.Value is not { } subject)
            return Result.Success<List<KpiSummaryDto>?>(null);

        var subjectType = subject.SubjectType;
        return await KpiSummaries.For(
            _dbContext,
            _dbContext.Kpis.Where(k => k.SubjectType == subjectType && k.SubjectId == request.SubjectId && k.Status == KpiStatus.Closed),
            cancellationToken);
    }
}
