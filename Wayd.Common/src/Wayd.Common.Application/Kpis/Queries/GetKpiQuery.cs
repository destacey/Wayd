using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis.Queries;

/// <summary>
/// One KPI with every measurement taken and its checkpoint plan, open or closed. Null when the record or the KPI does not exist, the KPI is on
/// another record, or the caller cannot read the record.
/// </summary>
/// <param name="SubjectType">The record's registered subject type.</param>
/// <param name="SubjectId">The record's id.</param>
/// <param name="KpiId">The KPI's id.</param>
public sealed record GetKpiQuery(string SubjectType, Guid SubjectId, Guid KpiId) : IQuery<Result<KpiDetailsDto?>>;

/// <summary>Rejects malformed input before the record is resolved.</summary>
public sealed class GetKpiQueryValidator : CustomValidator<GetKpiQuery>
{
    public GetKpiQueryValidator()
    {
        RuleFor(q => q.SubjectType).NotEmpty().MaximumLength(KpiSubjectTypeDescriptor.KeyMaxLength);
        RuleFor(q => q.SubjectId).NotEmpty();
        RuleFor(q => q.KpiId).NotEmpty();
    }
}

/// <summary>Reads the KPI and its history once the record's subject type allows it.</summary>
public sealed class GetKpiQueryHandler(KpiSubjectAccessResolver accessResolver, IKpisDbContext dbContext)
    : IQueryHandler<GetKpiQuery, Result<KpiDetailsDto?>>
{
    private readonly KpiSubjectAccessResolver _accessResolver = accessResolver;
    private readonly IKpisDbContext _dbContext = dbContext;

    public async Task<Result<KpiDetailsDto?>> Handle(GetKpiQuery request, CancellationToken cancellationToken)
    {
        var resolved = await _accessResolver.Resolve(request.SubjectType, request.SubjectId, cancellationToken);
        if (resolved.IsFailure)
            return Result.Failure<KpiDetailsDto?>(resolved.Error);

        if (resolved.Value is not { } subject)
            return Result.Success<KpiDetailsDto?>(null);

        var subjectType = subject.SubjectType;
        var kpi = await _dbContext.Kpis
            .AsNoTracking()
            .Include(k => k.Measurements)
            .Include(k => k.Checkpoints)
            .FirstOrDefaultAsync(k => k.Id == request.KpiId && k.SubjectType == subjectType && k.SubjectId == request.SubjectId, cancellationToken);
        if (kpi is null)
            return Result.Success<KpiDetailsDto?>(null);

        var category = kpi.CategoryId is { } categoryId
            ? await _dbContext.KpiCategories
                .Where(c => c.Id == categoryId)
                .Select(c => new KpiCategoryRefDto(c.Id, c.Name, c.Order))
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var newestFirst = kpi.Measurements.OrderByDescending(m => m.PeriodEnd).ThenByDescending(m => m.MeasuredAt).ToList();

        var takerIds = newestFirst.Where(m => m.MeasuredById.HasValue).Select(m => m.MeasuredById!.Value).Distinct().ToList();
        var takers = takerIds.Count == 0
            ? new Dictionary<Guid, EmployeeNavigationDto>()
            : (await _dbContext.Employees.AsNoTracking().Where(e => takerIds.Contains(e.Id)).ToListAsync(cancellationToken))
                .ToDictionary(e => e.Id, EmployeeNavigationDto.From);

        var points = newestFirst.Select(m => new KpiPointDto(m.PeriodStart, m.PeriodEnd, m.Value, m.MeasuredAt)).ToList();
        var recent = points.Take(KpiSummaries.ReadValueCount).ToList();

        return new KpiDetailsDto
        {
            Kpi = KpiSummaries.Summary(kpi, category, recent),
            SubjectName = subject.Access.Name,
            CanManage = subject.Access.CanManage,
            Measurements = newestFirst.Select(m => KpiMeasurementDto.From(m, takers)).ToList(),
            CheckpointReadings = KpiSummaries.CheckpointReadings(kpi, points),
        };
    }
}
