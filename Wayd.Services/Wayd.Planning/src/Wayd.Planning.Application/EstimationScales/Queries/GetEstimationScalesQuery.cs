using Wayd.Planning.Application.EstimationScales.Dtos;

namespace Wayd.Planning.Application.EstimationScales.Queries;

public sealed record GetEstimationScalesQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<EstimationScaleDto>>;

public sealed class GetEstimationScalesQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetEstimationScalesQuery, IReadOnlyList<EstimationScaleDto>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<IReadOnlyList<EstimationScaleDto>> Handle(GetEstimationScalesQuery request, CancellationToken cancellationToken)
    {
        var query = _planningDbContext.EstimationScales.AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .ProjectToType<EstimationScaleDto>()
            .ToListAsync(cancellationToken);
    }
}
