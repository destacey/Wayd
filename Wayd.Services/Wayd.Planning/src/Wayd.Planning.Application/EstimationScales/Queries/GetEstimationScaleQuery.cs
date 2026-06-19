using Wayd.Planning.Application.EstimationScales.Dtos;

namespace Wayd.Planning.Application.EstimationScales.Queries;

public sealed record GetEstimationScaleQuery(int Id) : IQuery<EstimationScaleDto?>;

internal sealed class GetEstimationScaleQueryHandler(IPlanningDbContext planningDbContext) : IQueryHandler<GetEstimationScaleQuery, EstimationScaleDto?>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    public async Task<EstimationScaleDto?> Handle(GetEstimationScaleQuery request, CancellationToken cancellationToken)
    {
        return await _planningDbContext.EstimationScales
            .Where(s => s.Id == request.Id)
            .ProjectToType<EstimationScaleDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
